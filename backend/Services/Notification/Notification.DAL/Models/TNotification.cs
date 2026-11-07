namespace Notification.DAL.Models;

/// <summary>Row of the Notifications table. The BLL works with Notification.Entities.NotificationItem instead.</summary>
public class TNotification
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public Guid? ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? Snippet { get; set; }
    public string Link { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string DedupKey { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
