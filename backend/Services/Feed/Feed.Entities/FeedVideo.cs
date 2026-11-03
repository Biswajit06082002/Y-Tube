namespace Feed.Entities;

/// <summary>
/// Shape of a video as the Video service returns it. Feed keeps its own copy of the contract
/// instead of referencing Video's projects — services share data over HTTP, not through code.
/// </summary>
public sealed record FeedVideo(
    string Id,
    string Title,
    string Description,
    string Category,
    Guid? OwnerId,
    string ChannelId,
    string ChannelName,
    long Views,
    int DurationSeconds,
    DateTime PublishedAtUtc,
    int LikeCount,
    bool LikedByMe,
    IReadOnlyList<string> Tags,
    string? ThumbnailUrl,
    string? StreamUrl,
    IReadOnlyList<string> Qualities,
    string Status);
