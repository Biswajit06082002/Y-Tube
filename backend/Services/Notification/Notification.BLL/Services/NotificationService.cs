using Notification.BLL.Interfaces;
using Notification.Entities;
using Notification.Entities.Contracts;

namespace Notification.BLL.Services;

/// <summary>Reading notifications and marking them read.</summary>
public sealed class NotificationService(INotificationRepository notifications) : INotificationService
{
    private const int PageSize = 20;

    public async Task<NotificationPage> GetPageAsync(Guid userId, long? cursor, CancellationToken ct = default)
    {
        // One extra row tells us whether there's a next page without a COUNT.
        var rows = await notifications.GetPageAsync(userId, cursor, PageSize + 1, ct);
        var hasMore = rows.Count > PageSize;
        var items = hasMore ? rows.Take(PageSize).ToList() : rows;
        int? unread = cursor is null ? await notifications.CountUnreadAsync(userId, ct) : null;

        return new NotificationPage([.. items.Select(ToDto)], hasMore ? items[^1].Id : null, unread);
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default) =>
        notifications.CountUnreadAsync(userId, ct);

    public Task MarkReadAsync(Guid userId, long id, CancellationToken ct = default) =>
        notifications.MarkReadAsync(userId, id, ct);

    public Task MarkAllReadAsync(Guid userId, CancellationToken ct = default) =>
        notifications.MarkAllReadAsync(userId, ct);

    public static NotificationDto ToDto(NotificationItem n) => new(
        n.Id, n.Type.ToString(), n.ActorId, n.ActorName, n.Text, n.Snippet, n.Link, n.ImageUrl, n.IsRead, n.CreatedAtUtc);
}
