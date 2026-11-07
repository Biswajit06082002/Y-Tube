using Notification.Entities;
using Notification.Entities.Contracts;
using YTube.Contracts.Events;

namespace Notification.BLL.Interfaces;

// --- Implemented in the BLL (business logic) ---

public interface INotificationService
{
    Task<NotificationPage> GetPageAsync(Guid userId, long? cursor, CancellationToken ct = default);
    Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default);
    Task MarkReadAsync(Guid userId, long id, CancellationToken ct = default);
    Task MarkAllReadAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>Turns events from other services into notifications for the right people.</summary>
public interface INotificationEventsHandler
{
    Task HandleAsync(VideoPublished e, CancellationToken ct = default);
    Task HandleAsync(UserFollowed e, CancellationToken ct = default);
    Task HandleAsync(UserMentioned e, CancellationToken ct = default);
    Task HandleAsync(CommentCreated e, CancellationToken ct = default);
}

// --- Implemented in the DAL / the API host ---

public interface INotificationRepository
{
    /// <summary>
    /// Saves the notifications, skipping any whose (UserId, DedupKey) already exists.
    /// Returns only the ones actually inserted (with ids) — those are the ones to push live.
    /// </summary>
    Task<IReadOnlyList<NotificationItem>> AddNewAsync(IReadOnlyList<NotificationItem> items, CancellationToken ct = default);

    /// <summary>Newest first, Id &lt; beforeId when given.</summary>
    Task<IReadOnlyList<NotificationItem>> GetPageAsync(Guid userId, long? beforeId, int take, CancellationToken ct = default);

    Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default);
    Task MarkReadAsync(Guid userId, long id, CancellationToken ct = default);
    Task MarkAllReadAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>User service: who follows a creator.</summary>
public interface IFollowersClient
{
    Task<IReadOnlyList<Guid>> GetFollowerIdsAsync(Guid creatorId, CancellationToken ct = default);
}

/// <summary>Sends notifications to connected browsers right away (SignalR, implemented in the API host).</summary>
public interface INotificationPusher
{
    Task PushAsync(IReadOnlyList<NotificationItem> items, CancellationToken ct = default);
}
