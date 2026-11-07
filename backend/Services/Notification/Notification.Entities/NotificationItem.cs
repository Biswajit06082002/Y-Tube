namespace Notification.Entities;

public enum NotificationType
{
    /// <summary>A creator you follow published a video.</summary>
    NewVideo,

    /// <summary>Your own upload finished processing and is live.</summary>
    VideoLive,

    /// <summary>Someone tagged you with @handle in a comment.</summary>
    Mention,

    /// <summary>Someone replied in a thread you started.</summary>
    Reply,

    /// <summary>Someone commented on your video.</summary>
    VideoComment,

    /// <summary>Someone started following you.</summary>
    NewFollower
}

/// <summary>
/// One notification for one user. Each recipient gets their own row ("fan-out on write"), so reading
/// your notifications is a single indexed query no matter how many people a creator has.
/// </summary>
public class NotificationItem
{
    /// <summary>Always increasing: also the cursor for paging.</summary>
    public long Id { get; set; }

    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }

    /// <summary>Who caused it (creator, commenter, follower) — for the avatar.</summary>
    public Guid? ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;

    /// <summary>The headline, e.g. "Sam replied to your comment on “My first vlog”".</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Extra line, e.g. the comment text.</summary>
    public string? Snippet { get; set; }

    /// <summary>Where clicking it goes (an app route like /watch/abc).</summary>
    public string Link { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    /// <summary>
    /// Identifies the event that caused it (e.g. "video:abc"). Unique per user, so a message RabbitMQ
    /// delivers twice still produces one notification (idempotent consumer).
    /// </summary>
    public string DedupKey { get; set; } = string.Empty;

    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
