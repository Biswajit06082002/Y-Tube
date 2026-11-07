namespace Comment.Entities;

/// <summary>
/// A comment or a reply. Replies are one level deep (like YouTube): ParentId always points at a
/// top-level comment, and replying to a reply just @mentions its author under the same thread.
/// </summary>
public class CommentItem
{
    /// <summary>Database identity: always increasing, so it doubles as the cursor for paging.</summary>
    public long Id { get; set; }

    public string VideoId { get; set; } = string.Empty;
    public long? ParentId { get; set; }

    public Guid AuthorId { get; set; }

    /// <summary>Copy of the author's name/@handle (denormalised; refreshed from user.profile.updated).</summary>
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorHandle { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    /// <summary>Stored counter, so a page of comments doesn't need a COUNT per comment.</summary>
    public int ReplyCount { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? EditedAtUtc { get; set; }

    public List<CommentMention> Mentions { get; set; } = [];
}

/// <summary>A user tagged with @handle in a comment, resolved to their id when the comment was saved.</summary>
public sealed record CommentMention(Guid UserId, string Handle);
