namespace Video.DAL.Models;

/// <summary>One like (VideoLikes table). Primary key (VideoId, UserId) makes a double-like impossible.</summary>
public class TVideoLike
{
    public string VideoId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
