namespace Notification.Entities.Contracts;

/// <summary>A notification as the API and the real-time hub send it.</summary>
public sealed record NotificationDto(
    long Id,
    string Type,
    Guid? ActorId,
    string ActorName,
    string Text,
    string? Snippet,
    string Link,
    string? ImageUrl,
    bool IsRead,
    DateTime CreatedAtUtc);

/// <summary>One page (newest first) with a cursor; UnreadCount is only filled on the first page.</summary>
public sealed record NotificationPage(IReadOnlyList<NotificationDto> Items, long? NextCursor, int? UnreadCount);

public sealed record UnreadCountDto(int UnreadCount);
