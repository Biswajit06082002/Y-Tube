using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Video.BLL.Interfaces;
using Video.Entities;
using Video.Entities.Contracts;
using YTube.Common.Messaging;
using YTube.Common.Responses;
using YTube.Common.Results;
using YTube.Contracts.Events;

namespace Video.BLL.Services;

/// <summary>
/// Video business rules: what's public, what only the owner sees, how uploads are validated,
/// stored and handed to the processing pipeline.
/// </summary>
public sealed class VideoService(
    IVideoRepository repository,
    IMediaStorage storage,
    IMessagePublisher publisher,
    ILogger<VideoService> logger) : IVideoService
{
    private const int MaxPageSize = 50;
    private const string MediaUrlPrefix = "/media/";

    public async Task<PagedResult<VideoDto>> ListAsync(int page, int pageSize, string? category, string? userId, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var (items, total) = await repository.ListReadyAsync((page - 1) * pageSize, pageSize, category, ct);
        return new PagedResult<VideoDto>(await ToDtosAsync(items, userId, ct), page, pageSize, total);
    }

    public IReadOnlyList<string> Categories() => UploadRules.Categories;

    public async Task<VideoDto?> GetAsync(string id, string? userId, CancellationToken ct = default)
    {
        var video = await repository.GetByIdAsync(id, ct);
        if (video is null || !CanView(video, userId)) return null;
        return (await ToDtosAsync([video], userId, ct))[0];
    }

    public async Task<IReadOnlyList<VideoDto>> LikedByAsync(string userId, CancellationToken ct = default)
    {
        var ids = await repository.GetLikedVideoIdsAsync(userId, ct);
        var videos = (await repository.GetByIdsAsync(ids, ct)).Where(v => v.Status == VideoStatus.Ready).ToList();
        return await ToDtosAsync(videos, userId, ct);
    }

    public async Task<IReadOnlyList<VideoDto>> ListMineAsync(Guid ownerId, CancellationToken ct = default) =>
        await ToDtosAsync(await repository.ListByOwnerAsync(ownerId, ct), ownerId.ToString(), ct);

    public async Task<VideoDto?> SetLikeAsync(string id, string userId, bool liked, CancellationToken ct = default)
    {
        var video = await repository.GetByIdAsync(id, ct);
        if (video is null || video.Status != VideoStatus.Ready) return null;

        await repository.SetLikeAsync(id, userId, liked, ct);
        return (await ToDtosAsync([video], userId, ct))[0];
    }

    public async Task<Result<VideoDto>> UploadAsync(UploadVideoCommand command, CancellationToken ct = default)
    {
        var validation = Validate(command, out var tags, out var videoExt, out var thumbExt);
        if (validation is not null) return validation;

        var video = new VideoItem
        {
            Id = NewVideoId(),
            OwnerId = command.OwnerId,
            Title = command.Title.Trim(),
            Description = command.Description?.Trim() ?? string.Empty,
            Category = command.Category,
            Tags = tags,
            ChannelId = $"ch_{command.OwnerId:N}"[..15],
            ChannelName = command.OwnerName,
            Status = VideoStatus.Queued,
            CreatedAtUtc = DateTime.UtcNow
        };

        // 1. Files first: the original (needed by the worker) and the optional custom thumbnail.
        video.SourcePath = await storage.SaveSourceAsync(video.Id, command.Video!.Content, videoExt!, ct);
        if (command.Thumbnail is not null)
            video.ThumbnailPath = await storage.SaveThumbnailAsync(video.Id, command.Thumbnail.Content, thumbExt!, ct);

        // 2. Then the database row, so the owner can already see "Queued".
        await repository.AddAsync(video, ct);

        // 3. Finally hand the job to the processing pipeline.
        try
        {
            await publisher.PublishAsync(RoutingKeys.VideoUploaded,
                new VideoUploaded(video.Id, video.SourcePath, video.ThumbnailPath is not null, video.CreatedAtUtc), ct);
        }
        catch (Exception ex)
        {
            // Without the message no worker will ever pick it up. (The "outbox pattern" removes
            // this gap by saving the message in the same DB transaction — a later lesson.)
            logger.LogError(ex, "Could not queue video {VideoId} for processing", video.Id);
            await repository.UpdateStatusAsync(video.Id, VideoStatus.Failed, 0, "Could not queue the video for processing.", ct);
            return Error.Unavailable("queue_unavailable", "The upload was saved but processing is unavailable right now. Please try again later.");
        }

        logger.LogInformation("Video {VideoId} uploaded by {OwnerId} and queued", video.Id, command.OwnerId);
        return (await ToDtosAsync([video], command.OwnerId.ToString(), ct))[0];
    }

    private static Error? Validate(UploadVideoCommand c, out List<string> tags, out string? videoExt, out string? thumbExt)
    {
        tags = [];
        videoExt = Path.GetExtension(c.Video?.FileName ?? string.Empty).ToLowerInvariant();
        thumbExt = c.Thumbnail is null ? null : Path.GetExtension(c.Thumbnail.FileName).ToLowerInvariant();

        if (c.Video is null || c.Video.Length == 0)
            return Error.Validation("video_required", "Please choose a video file.");
        if (!UploadRules.VideoExtensions.Contains(videoExt))
            return Error.Validation("video_type", $"Supported video types: {string.Join(", ", UploadRules.VideoExtensions)}.");
        if (c.Video.Length > UploadRules.MaxVideoBytes)
            return Error.Validation("video_too_large", "Videos can be at most 2 GB.");

        if (c.Thumbnail is not null)
        {
            if (!UploadRules.ThumbnailExtensions.Contains(thumbExt))
                return Error.Validation("thumbnail_type", "Thumbnails must be JPG, PNG or WebP.");
            if (c.Thumbnail.Length > UploadRules.MaxThumbnailBytes)
                return Error.Validation("thumbnail_too_large", "Thumbnails can be at most 5 MB.");
        }

        var title = c.Title?.Trim() ?? string.Empty;
        if (title.Length == 0)
            return Error.Validation("title_required", "Please add a title.");
        if (title.Length > UploadRules.MaxTitleLength)
            return Error.Validation("title_too_long", $"Titles can be at most {UploadRules.MaxTitleLength} characters.");
        if ((c.Description?.Length ?? 0) > UploadRules.MaxDescriptionLength)
            return Error.Validation("description_too_long", $"Descriptions can be at most {UploadRules.MaxDescriptionLength} characters.");
        if (!UploadRules.Categories.Contains(c.Category))
            return Error.Validation("category_invalid", "Please choose a category.");

        // Tags: trimmed, lower-case, no '#', unique — so "Angular", "#angular" and "angular " are one tag.
        tags = c.Tags
            .Select(t => t.Trim().TrimStart('#').ToLowerInvariant())
            .Where(t => t.Length > 0)
            .Distinct()
            .ToList();
        if (tags.Count > UploadRules.MaxTags)
            return Error.Validation("too_many_tags", $"You can add up to {UploadRules.MaxTags} tags.");
        if (tags.Any(t => t.Length > UploadRules.MaxTagLength))
            return Error.Validation("tag_too_long", $"Tags can be at most {UploadRules.MaxTagLength} characters.");

        return null;
    }

    private static bool CanView(VideoItem video, string? userId) =>
        video.Status == VideoStatus.Ready || (userId is not null && video.OwnerId?.ToString() == userId);

    /// <summary>Builds DTOs for a batch of videos with two like queries in total (not two per video).</summary>
    private async Task<IReadOnlyList<VideoDto>> ToDtosAsync(IReadOnlyList<VideoItem> videos, string? userId, CancellationToken ct)
    {
        if (videos.Count == 0) return [];

        var ids = videos.Select(v => v.Id).ToList();
        var likeCounts = await repository.CountLikesAsync(ids, ct);
        var likedByMe = userId is null ? new HashSet<string>() : await repository.GetLikedAmongAsync(userId, ids, ct);

        return [.. videos.Select(v => new VideoDto(
            v.Id, v.Title, v.Description, v.Category, v.Tags, v.ChannelId, v.ChannelName, v.Views, v.DurationSeconds,
            v.PublishedAtUtc ?? v.CreatedAtUtc,
            v.BaseLikes + likeCounts.GetValueOrDefault(v.Id),
            likedByMe.Contains(v.Id),
            userId is not null && v.OwnerId?.ToString() == userId,
            v.Status, v.ProcessingProgress, v.ProcessingError,
            v.ThumbnailPath is null ? null : MediaUrlPrefix + v.ThumbnailPath,
            v.Status == VideoStatus.Ready && v.MasterPlaylistPath is not null ? MediaUrlPrefix + v.MasterPlaylistPath : null,
            v.Renditions))];
    }

    /// <summary>11-character URL-safe id, like YouTube's. Cryptographically random so ids can't be guessed.</summary>
    private static string NewVideoId() =>
        new(RandomNumberGenerator.GetItems("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_".AsSpan(), 11));
}
