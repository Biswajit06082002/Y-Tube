namespace Video.DAL.Models;

/// <summary>
/// One tag of one video (VideoTags table). A separate table — not a comma-separated column —
/// so "all videos tagged 'angular'" is an indexed lookup instead of a full table scan.
/// </summary>
public class TVideoTag
{
    public string VideoId { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;

    public TVideo? Video { get; set; }
}
