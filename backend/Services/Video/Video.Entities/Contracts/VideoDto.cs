namespace Video.Entities.Contracts;

/// <summary>A video as the API returns it, personalised for the caller (LikedByMe).</summary>
public sealed record VideoDto(
    string Id,
    string Title,
    string Description,
    string Category,
    string ChannelId,
    string ChannelName,
    long Views,
    int DurationSeconds,
    DateTime PublishedAtUtc,
    int LikeCount,
    bool LikedByMe);
