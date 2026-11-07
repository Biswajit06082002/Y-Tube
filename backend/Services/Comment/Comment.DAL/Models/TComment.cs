namespace Comment.DAL.Models;

/// <summary>Row of the Comments table. The BLL works with Comment.Entities.CommentItem instead.</summary>
public class TComment
{
    public long Id { get; set; }
    public string VideoId { get; set; } = string.Empty;
    public long? ParentId { get; set; }
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorHandle { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public int ReplyCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? EditedAtUtc { get; set; }

    public List<TCommentMention> Mentions { get; set; } = [];
}
