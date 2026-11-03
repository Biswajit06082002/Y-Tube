namespace Feed.DAL.Models;

/// <summary>
/// The JSON shape the Video service returns. Kept in the DAL so a change in Video's API only
/// touches this model and its mapping — Feed's BLL keeps working with Feed.Entities.FeedVideo.
/// </summary>
public class TVideoResponse
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public Guid? OwnerId { get; set; }
    public string ChannelId { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public long Views { get; set; }
    public int DurationSeconds { get; set; }
    public DateTime PublishedAtUtc { get; set; }
    public int LikeCount { get; set; }
    public bool LikedByMe { get; set; }
    public List<string> Tags { get; set; } = [];
    public string? ThumbnailUrl { get; set; }
    public string? StreamUrl { get; set; }
    public List<string> Qualities { get; set; } = [];
    public string Status { get; set; } = string.Empty;
}
