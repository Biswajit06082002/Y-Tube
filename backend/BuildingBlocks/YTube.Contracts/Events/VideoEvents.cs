namespace YTube.Contracts.Events;

/// <summary>
/// Routing keys on the "ytube.events" topic exchange. Topic exchanges match patterns, so a
/// queue bound to "video.processing.*" receives progress, completed and failed events.
/// </summary>
public static class RoutingKeys
{
    public const string VideoUploaded = "video.uploaded";
    public const string ProcessingProgress = "video.processing.progress";
    public const string ProcessingCompleted = "video.processing.completed";
    public const string ProcessingFailed = "video.processing.failed";
    public const string VideoPublished = "video.published";
}

// Messages are the contract between services: they only carry data, never behaviour.
// Paths are relative to the media root (Storage:RootPath) so no service depends on another's disk layout.

/// <summary>Published by the Video service once the original file is safely stored.</summary>
public sealed record VideoUploaded(
    string VideoId,
    string SourcePath,
    bool HasCustomThumbnail,
    DateTime UploadedAtUtc);

/// <summary>Published by the Processing worker while transcoding (throttled).</summary>
public sealed record VideoProcessingProgress(string VideoId, int Percent, string Stage);

public sealed record RenditionInfo(string Name, int Width, int Height, int BitrateKbps);

/// <summary>Published by the Processing worker when every rendition and the thumbnail are ready.</summary>
public sealed record VideoProcessingCompleted(
    string VideoId,
    int DurationSeconds,
    int Width,
    int Height,
    IReadOnlyList<RenditionInfo> Renditions,
    string MasterPlaylistPath,
    string? ThumbnailPath);

public sealed record VideoProcessingFailed(string VideoId, string Reason);

/// <summary>
/// Published by the Video service when an uploaded video becomes watchable (processing finished).
/// The Notification service tells the creator and every follower.
/// </summary>
public sealed record VideoPublished(
    string VideoId,
    Guid OwnerId,
    string ChannelName,
    string Title,
    string? ThumbnailUrl,
    DateTime PublishedAtUtc);
