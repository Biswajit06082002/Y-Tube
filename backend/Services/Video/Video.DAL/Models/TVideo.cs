namespace Video.DAL.Models;

/// <summary>A stored video row. The BLL works with Video.Entities.VideoItem instead.</summary>
public class TVideo
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public long Views { get; set; }
    public int DurationSeconds { get; set; }
    public DateTime PublishedAtUtc { get; set; }
    public int BaseLikes { get; set; }
}
