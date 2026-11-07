using Microsoft.Extensions.Logging;
using Notification.BLL.Interfaces;
using Notification.Entities;
using YTube.Contracts.Events;

namespace Notification.BLL.Services;

/// <summary>
/// Decides who hears about what. Every notification is saved first (so nothing is lost when the user
/// is offline) and then pushed live to anyone connected. Saving is idempotent via DedupKey, and only
/// newly saved rows are pushed — a redelivered event changes nothing.
/// </summary>
public sealed class NotificationEventsHandler(
    INotificationRepository notifications,
    IFollowersClient followers,
    INotificationPusher pusher,
    ILogger<NotificationEventsHandler> logger) : INotificationEventsHandler
{
    /// <summary>Insert/push followers in chunks so a big channel doesn't build one giant transaction.</summary>
    private const int FanOutChunk = 500;

    public async Task HandleAsync(VideoPublished e, CancellationToken ct = default)
    {
        // The creator: "your video is live".
        await SaveAndPushAsync([new NotificationItem
        {
            UserId = e.OwnerId,
            Type = NotificationType.VideoLive,
            ActorId = e.OwnerId,
            ActorName = e.ChannelName,
            Text = $"Your video is live: {e.Title}",
            Snippet = "Processing finished. It's now on your channel and in your followers' feeds.",
            Link = $"/watch/{e.VideoId}",
            ImageUrl = e.ThumbnailUrl,
            DedupKey = $"video-live:{e.VideoId}",
            CreatedAtUtc = e.PublishedAtUtc
        }], ct);

        // Every follower: fan-out on write. If the User service is down this throws, the message goes
        // to the dead-letter queue, and can be replayed — the DedupKey makes the replay safe.
        var followerIds = await followers.GetFollowerIdsAsync(e.OwnerId, ct);
        foreach (var chunk in followerIds.Where(id => id != e.OwnerId).Chunk(FanOutChunk))
        {
            await SaveAndPushAsync([.. chunk.Select(followerId => new NotificationItem
            {
                UserId = followerId,
                Type = NotificationType.NewVideo,
                ActorId = e.OwnerId,
                ActorName = e.ChannelName,
                Text = $"{e.ChannelName} uploaded: {e.Title}",
                Link = $"/watch/{e.VideoId}",
                ImageUrl = e.ThumbnailUrl,
                DedupKey = $"video:{e.VideoId}",
                CreatedAtUtc = e.PublishedAtUtc
            })], ct);
        }

        logger.LogInformation("Video {VideoId}: notified the creator and {Count} follower(s)", e.VideoId, followerIds.Count);
    }

    public Task HandleAsync(UserFollowed e, CancellationToken ct = default) =>
        SaveAndPushAsync([new NotificationItem
        {
            UserId = e.FolloweeId,
            Type = NotificationType.NewFollower,
            ActorId = e.FollowerId,
            ActorName = e.FollowerName,
            Text = string.IsNullOrEmpty(e.FollowerHandle)
                ? $"{e.FollowerName} started following you"
                : $"{e.FollowerName} (@{e.FollowerHandle}) started following you",
            Link = $"/channel/{e.FollowerId}",
            // Once per follower: unfollow + follow again doesn't notify twice (no spam by toggling).
            DedupKey = $"follow:{e.FollowerId}",
            CreatedAtUtc = e.FollowedAtUtc
        }], ct);

    public Task HandleAsync(UserMentioned e, CancellationToken ct = default) =>
        e.MentionedUserId == e.AuthorId
            ? Task.CompletedTask
            : SaveAndPushAsync([new NotificationItem
            {
                UserId = e.MentionedUserId,
                Type = NotificationType.Mention,
                ActorId = e.AuthorId,
                ActorName = e.AuthorName,
                Text = $"{e.AuthorName} mentioned you on “{e.VideoTitle}”",
                Snippet = e.Snippet,
                Link = $"/watch/{e.VideoId}",
                DedupKey = $"mention:{e.CommentId}",
                CreatedAtUtc = e.MentionedAtUtc
            }], ct);

    public Task HandleAsync(CommentCreated e, CancellationToken ct = default)
    {
        // Who should hear about it? Never the author, and never someone who's mentioned in it —
        // they already get "mentioned you" for this same comment.
        bool ShouldNotify(Guid? userId) => userId is { } id && id != e.AuthorId && !e.MentionedUserIds.Contains(id);

        var items = new List<NotificationItem>();
        if (e.ParentId is not null && ShouldNotify(e.ParentAuthorId))
        {
            items.Add(new NotificationItem
            {
                UserId = e.ParentAuthorId!.Value,
                Type = NotificationType.Reply,
                ActorId = e.AuthorId,
                ActorName = e.AuthorName,
                Text = $"{e.AuthorName} replied to your comment on “{e.VideoTitle}”",
                Snippet = e.Snippet,
                Link = $"/watch/{e.VideoId}",
                DedupKey = $"comment:{e.CommentId}",
                CreatedAtUtc = e.CreatedAtUtc
            });
        }

        // The creator hears about new top-level comments (and replies, unless they already got one above).
        if (ShouldNotify(e.VideoOwnerId) && !items.Any(i => i.UserId == e.VideoOwnerId))
        {
            items.Add(new NotificationItem
            {
                UserId = e.VideoOwnerId!.Value,
                Type = NotificationType.VideoComment,
                ActorId = e.AuthorId,
                ActorName = e.AuthorName,
                Text = e.ParentId is null
                    ? $"{e.AuthorName} commented on your video “{e.VideoTitle}”"
                    : $"{e.AuthorName} replied to a comment on your video “{e.VideoTitle}”",
                Snippet = e.Snippet,
                Link = $"/watch/{e.VideoId}",
                DedupKey = $"comment:{e.CommentId}",
                CreatedAtUtc = e.CreatedAtUtc
            });
        }

        return items.Count == 0 ? Task.CompletedTask : SaveAndPushAsync(items, ct);
    }

    private async Task SaveAndPushAsync(IReadOnlyList<NotificationItem> items, CancellationToken ct)
    {
        var saved = await notifications.AddNewAsync(items, ct);
        if (saved.Count == 0) return; // all duplicates: this event was already handled

        try
        {
            await pusher.PushAsync(saved, ct);
        }
        catch (Exception ex)
        {
            // Saved is what matters: the bell picks them up the next time the app loads or reconnects.
            logger.LogWarning(ex, "Saved {Count} notification(s) but could not push them live", saved.Count);
        }
    }
}
