using System.Text.RegularExpressions;
using Comment.BLL.Interfaces;
using Comment.Entities;
using Comment.Entities.Contracts;
using Microsoft.Extensions.Logging;
using YTube.Common.Messaging;
using YTube.Common.Results;
using YTube.Contracts.Events;

namespace Comment.BLL.Services;

/// <summary>Comments, one level of replies and @mentions.</summary>
public sealed partial class CommentService(
    ICommentRepository comments,
    IVideoClient videos,
    IUserClient users,
    IMessagePublisher publisher,
    ILogger<CommentService> logger) : ICommentService
{
    public const int MaxLength = 2000;
    private const int MaxMentions = 10;
    private const int PageSize = 20;
    private const int RepliesPageSize = 10;

    // "@handle" not preceded by a letter/digit/@ — so "me@mail.com" is not a mention.
    [GeneratedRegex(@"(?<![A-Za-z0-9_.@])@([A-Za-z0-9_.]{3,30})")]
    private static partial Regex MentionPattern();

    public async Task<Result<CommentPage>> GetCommentsAsync(string videoId, long? cursor, Guid? viewerId, CancellationToken ct = default)
    {
        var rows = await comments.GetTopLevelAsync(videoId, cursor, PageSize + 1, ct);
        int? total = cursor is null ? await comments.CountAsync(videoId, ct) : null;
        return await ToPageAsync(videoId, rows, PageSize, total, viewerId, ct);
    }

    public async Task<Result<CommentPage>> GetRepliesAsync(long commentId, long? cursor, Guid? viewerId, CancellationToken ct = default)
    {
        var parent = await comments.GetAsync(commentId, ct);
        if (parent is null) return Error.NotFound("comment_not_found", "This comment was deleted.");

        var rows = await comments.GetRepliesAsync(commentId, cursor, RepliesPageSize + 1, ct);
        return await ToPageAsync(parent.VideoId, rows, RepliesPageSize, null, viewerId, ct);
    }

    public async Task<Result<CommentDto>> PostAsync(string videoId, Guid authorId, PostCommentRequest request, CancellationToken ct = default)
    {
        var text = Clean(request.Text);
        if (Validate(text) is { } invalid) return invalid;

        VideoInfo? video;
        AuthorInfo? author;
        try
        {
            // Two independent calls to other services — run them at the same time.
            var videoTask = videos.GetAsync(videoId, ct);
            var authorTask = users.GetMeAsync(ct);
            video = await videoTask;
            author = await authorTask;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Video/User service unavailable while posting a comment on {VideoId}", videoId);
            return Error.Unavailable("dependency_unavailable", "Comments are temporarily unavailable. Please try again.");
        }

        if (video is null) return Error.NotFound("video_not_found", "This video doesn't exist.");
        if (author is null) return Error.Unauthorized("profile_missing", "Please sign in again.");

        long? parentId = null;
        Guid? threadAuthorId = null;
        if (request.ParentId is { } requested)
        {
            var parent = await comments.GetAsync(requested, ct);
            if (parent is null || parent.VideoId != videoId)
                return Error.NotFound("comment_not_found", "The comment you're replying to was deleted.");
            parentId = parent.ParentId ?? parent.Id; // reply to a reply → same thread, one level deep
            threadAuthorId = parent.ParentId is null ? parent.AuthorId : (await comments.GetAsync(parentId.Value, ct))?.AuthorId;
        }

        var comment = new CommentItem
        {
            VideoId = videoId,
            ParentId = parentId,
            AuthorId = authorId,
            AuthorName = author.DisplayName,
            AuthorHandle = author.Handle,
            Text = text,
            CreatedAtUtc = DateTime.UtcNow,
            Mentions = await ResolveMentionsAsync(text, ct)
        };
        await comments.AddAsync(comment, ct);

        await PublishCreatedAsync(comment, video, threadAuthorId, ct);
        await PublishMentionsAsync(comment, video.Title, comment.Mentions, ct);
        return ToDto(comment, video.OwnerId, authorId);
    }

    public async Task<Result<CommentDto>> EditAsync(long commentId, Guid userId, EditCommentRequest request, CancellationToken ct = default)
    {
        var comment = await comments.GetAsync(commentId, ct);
        if (comment is null) return Error.NotFound("comment_not_found", "This comment was deleted.");
        if (comment.AuthorId != userId) return Error.Validation("not_your_comment", "You can only edit your own comments.");

        var text = Clean(request.Text);
        if (Validate(text) is { } invalid) return invalid;

        var before = comment.Mentions.Select(m => m.UserId).ToHashSet();
        comment.Text = text;
        comment.EditedAtUtc = DateTime.UtcNow;
        comment.Mentions = await ResolveMentionsAsync(text, ct);
        await comments.UpdateTextAsync(comment.Id, comment.Text, comment.Mentions, comment.EditedAtUtc.Value, ct);

        // Only people who are newly tagged get notified.
        var video = await TryGetVideoAsync(comment.VideoId, ct);
        await PublishMentionsAsync(comment, video?.Title ?? "a video", [.. comment.Mentions.Where(m => !before.Contains(m.UserId))], ct);
        return ToDto(comment, video?.OwnerId, userId);
    }

    public async Task<Result<bool>> DeleteAsync(long commentId, Guid userId, CancellationToken ct = default)
    {
        var comment = await comments.GetAsync(commentId, ct);
        if (comment is null) return true; // already gone: deleting is idempotent

        if (comment.AuthorId != userId && await TryGetOwnerAsync(comment.VideoId, ct) != userId)
            return Error.Validation("cannot_delete", "Only the author or the video's creator can delete this comment.");

        await comments.DeleteAsync(comment, ct);
        return true;
    }

    private static string Clean(string? text) => (text ?? "").Replace("\r\n", "\n").Trim();

    private static Error? Validate(string text) =>
        text.Length == 0 ? Error.Validation("comment_empty", "Write something first.")
        : text.Length > MaxLength ? Error.Validation("comment_too_long", $"Comments can be at most {MaxLength} characters.")
        : null;

    /// <summary>Finds "@handle"s in the text and keeps the ones that belong to real users.</summary>
    private async Task<List<CommentMention>> ResolveMentionsAsync(string text, CancellationToken ct)
    {
        var handles = MentionPattern().Matches(text)
            .Select(m => m.Groups[1].Value.TrimEnd('.').ToLowerInvariant()) // "@sam." at the end of a sentence
            .Where(h => h.Length >= 3)
            .Distinct()
            .Take(MaxMentions)
            .ToList();
        if (handles.Count == 0) return [];

        try
        {
            var found = await users.GetByHandlesAsync(handles, ct);
            return [.. found.Select(u => new CommentMention(u.UserId, u.Handle))];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Degrade gracefully: the comment is still saved, the @handles just aren't links.
            logger.LogWarning(ex, "User service unavailable; saving comment without resolved mentions");
            return [];
        }
    }

    private static string Snippet(string text) => text.Length > 140 ? text[..140] + "…" : text;

    private async Task PublishCreatedAsync(CommentItem comment, VideoInfo video, Guid? threadAuthorId, CancellationToken ct)
    {
        try
        {
            await publisher.PublishAsync(CommentRoutingKeys.CommentCreated, new CommentCreated(
                comment.Id, comment.ParentId, comment.VideoId, video.Title, video.OwnerId, threadAuthorId,
                comment.AuthorId, comment.AuthorName, Snippet(comment.Text),
                [.. comment.Mentions.Select(m => m.UserId)], comment.CreatedAtUtc), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not publish CommentCreated for comment {CommentId}", comment.Id);
        }
    }

    private async Task PublishMentionsAsync(CommentItem comment, string videoTitle, IReadOnlyList<CommentMention> mentions, CancellationToken ct)
    {
        var snippet = Snippet(comment.Text);
        foreach (var mention in mentions.Where(m => m.UserId != comment.AuthorId))
        {
            try
            {
                await publisher.PublishAsync(CommentRoutingKeys.UserMentioned, new UserMentioned(
                    comment.Id, comment.VideoId, videoTitle, mention.UserId, comment.AuthorId, comment.AuthorName, snippet, DateTime.UtcNow), ct);
            }
            catch (Exception ex)
            {
                // The comment is saved; a lost notification is not worth failing the request for.
                logger.LogWarning(ex, "Could not publish UserMentioned for comment {CommentId}", comment.Id);
            }
        }
    }

    /// <summary>The video's creator, or null if unknown (sample video, or the Video service is down).</summary>
    private async Task<Guid?> TryGetOwnerAsync(string videoId, CancellationToken ct) =>
        (await TryGetVideoAsync(videoId, ct))?.OwnerId;

    private async Task<VideoInfo?> TryGetVideoAsync(string videoId, CancellationToken ct)
    {
        try
        {
            return await videos.GetAsync(videoId, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Video service unavailable; showing comments without creator info");
            return null;
        }
    }

    private async Task<Result<CommentPage>> ToPageAsync(
        string videoId, IReadOnlyList<CommentItem> rows, int pageSize, int? total, Guid? viewerId, CancellationToken ct)
    {
        // We asked for one extra row: if it came back, there is another page.
        var hasMore = rows.Count > pageSize;
        var items = hasMore ? rows.Take(pageSize).ToList() : rows;
        var owner = items.Count > 0 ? await TryGetOwnerAsync(videoId, ct) : null;

        return new CommentPage(
            [.. items.Select(c => ToDto(c, owner, viewerId))],
            hasMore ? items[^1].Id : null,
            total);
    }

    private static CommentDto ToDto(CommentItem c, Guid? videoOwnerId, Guid? viewerId) => new(
        c.Id, c.VideoId, c.ParentId, c.AuthorId, c.AuthorName, c.AuthorHandle,
        IsByCreator: videoOwnerId is not null && c.AuthorId == videoOwnerId,
        c.Text,
        [.. c.Mentions.Select(m => new MentionDto(m.UserId, m.Handle))],
        c.ReplyCount, c.CreatedAtUtc, c.EditedAtUtc,
        CanEdit: viewerId is not null && viewerId == c.AuthorId,
        CanDelete: viewerId is not null && (viewerId == c.AuthorId || viewerId == videoOwnerId));
}
