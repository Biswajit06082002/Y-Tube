namespace Video.Entities;

/// <summary>A video as the business layer sees it (the DAL maps it to/from the TVideo table model).</summary>
public class VideoItem
{
    public string Id { get; set; } = string.Empty;

    /// <summary>Uploader's user id (JWT "sub"). Null for the seeded sample videos.</summary>
    public Guid? OwnerId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];

    public string ChannelId { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;

    public long Views { get; set; }
    public int BaseLikes { get; set; }

    public VideoStatus Status { get; set; }
    public int ProcessingProgress { get; set; }
    public string? ProcessingError { get; set; }

    public int DurationSeconds { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>Available qualities, highest first, e.g. ["1080p", "720p", "480p"].</summary>
    public List<string> Renditions { get; set; } = [];

    // Paths are relative to the media root (D:\YTubeMedia), e.g. "videos/abc/hls/master.m3u8".
    public string? SourcePath { get; set; }
    public string? ThumbnailPath { get; set; }
    public string? MasterPlaylistPath { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
}
