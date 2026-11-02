namespace Video.DAL.Models;

/// <summary>Row of the Videos table. The BLL works with Video.Entities.VideoItem instead.</summary>
public class TVideo
{
    public string Id { get; set; } = string.Empty;
    public Guid? OwnerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public long Views { get; set; }
    public int BaseLikes { get; set; }

    /// <summary>Stored as int (VideoStatus).</summary>
    public int Status { get; set; }
    public int ProcessingProgress { get; set; }
    public string? ProcessingError { get; set; }

    public int DurationSeconds { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>Comma-separated, highest first: "1080p,720p,480p".</summary>
    public string Renditions { get; set; } = string.Empty;

    public string? SourcePath { get; set; }
    public string? ThumbnailPath { get; set; }
    public string? MasterPlaylistPath { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }

    public List<TVideoTag> Tags { get; set; } = [];
}
