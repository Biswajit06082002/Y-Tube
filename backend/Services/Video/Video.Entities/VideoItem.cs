namespace Video.Entities;

/// <summary>A video as it is stored (in memory for now; a database table in Phase 2).</summary>
public sealed record VideoItem(
    string Id,
    string Title,
    string Description,
    string Category,
    string ChannelId,
    string ChannelName,
    long Views,
    int DurationSeconds,
    DateTime PublishedAtUtc,
    int BaseLikes);
