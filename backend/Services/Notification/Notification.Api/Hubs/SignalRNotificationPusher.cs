using Microsoft.AspNetCore.SignalR;
using Notification.BLL.Interfaces;
using Notification.BLL.Services;
using Notification.Entities;

namespace Notification.Api.Hubs;

/// <summary>
/// Pushes saved notifications to their owners' open connections. Users who aren't connected
/// simply get nothing now — they read the saved rows when the app next loads.
/// </summary>
public sealed class SignalRNotificationPusher(IHubContext<NotificationsHub> hub) : INotificationPusher
{
    public async Task PushAsync(IReadOnlyList<NotificationItem> items, CancellationToken ct = default)
    {
        foreach (var item in items)
            await hub.Clients.User(item.UserId.ToString()).SendAsync(NotificationsHub.NotificationMethod, NotificationService.ToDto(item), ct);
    }
}
