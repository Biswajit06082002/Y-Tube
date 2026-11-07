using Notification.DAL.Models;
using Notification.Entities;

namespace Notification.DAL.Mappings;

/// <summary>Converts between table models (DAL) and business entities (BLL).</summary>
internal static class NotificationMappings
{
    public static NotificationItem ToEntity(this TNotification m) => new()
    {
        Id = m.Id,
        UserId = m.UserId,
        Type = Enum.TryParse<NotificationType>(m.Type, out var type) ? type : NotificationType.NewVideo,
        ActorId = m.ActorId,
        ActorName = m.ActorName,
        Text = m.Text,
        Snippet = m.Snippet,
        Link = m.Link,
        ImageUrl = m.ImageUrl,
        DedupKey = m.DedupKey,
        IsRead = m.IsRead,
        CreatedAtUtc = m.CreatedAtUtc
    };

    public static TNotification ToModel(this NotificationItem e) => new()
    {
        UserId = e.UserId,
        Type = e.Type.ToString(),
        ActorId = e.ActorId,
        ActorName = Truncate(e.ActorName, 50),
        Text = Truncate(e.Text, 300),
        Snippet = e.Snippet is null ? null : Truncate(e.Snippet, 300),
        Link = e.Link,
        ImageUrl = e.ImageUrl,
        DedupKey = e.DedupKey,
        IsRead = e.IsRead,
        CreatedAtUtc = e.CreatedAtUtc
    };

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
}
