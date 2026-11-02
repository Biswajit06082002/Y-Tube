namespace Video.Entities.Contracts;

/// <summary>A video as the API returns it, personalised for the caller (LikedByMe, IsOwner).</summary>
public sealed record VideoDto(
    string Id,
    string Title,
    string Description,
    string Category,
    IReadOnlyList<string> Tags,
    string ChannelId,
    string ChannelName,
    long Views,
    int DurationSeconds,
    DateTime PublishedAtUtc,
    int LikeCount,
    bool LikedByMe,
    bool IsOwner,
    VideoStatus Status,
    int ProcessingProgress,
    string? ProcessingError,
    string? ThumbnailUrl,
    string? StreamUrl,
    IReadOnlyList<string> Qualities);
