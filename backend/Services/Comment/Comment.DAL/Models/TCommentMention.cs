namespace Comment.DAL.Models;

/// <summary>Row of the CommentMentions table: "comment X tags user Y".</summary>
public class TCommentMention
{
    public long CommentId { get; set; }
    public Guid UserId { get; set; }
    public string Handle { get; set; } = string.Empty;
}
