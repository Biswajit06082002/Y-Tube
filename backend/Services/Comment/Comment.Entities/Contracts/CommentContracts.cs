namespace Comment.Entities.Contracts;

/// <summary>A comment as a particular viewer sees it (CanEdit / CanDelete depend on who asks).</summary>
public sealed record CommentDto(
    long Id,
    string VideoId,
    long? ParentId,
    Guid AuthorId,
    string AuthorName,
    string AuthorHandle,
    bool IsByCreator,
    string Text,
    IReadOnlyList<MentionDto> Mentions,
    int ReplyCount,
    DateTime CreatedAtUtc,
    DateTime? EditedAtUtc,
    bool CanEdit,
    bool CanDelete);

public sealed record MentionDto(Guid UserId, string Handle);

/// <summary>
/// One page of comments with a cursor (keyset paging). Pass NextCursor back to get the next page;
/// null means there is nothing more. TotalCount is only filled on the first page.
/// </summary>
public sealed record CommentPage(IReadOnlyList<CommentDto> Items, long? NextCursor, int? TotalCount);

/// <param name="ParentId">Set to reply. Replying to a reply is allowed; it lands in the same thread.</param>
public sealed record PostCommentRequest(string Text, long? ParentId);

public sealed record EditCommentRequest(string Text);

/// <summary>The video facts the Comment service needs (from the Video service).</summary>
public sealed record VideoInfo(string Id, Guid? OwnerId, string Title);

/// <summary>Who is writing (from the User service).</summary>
public sealed record AuthorInfo(Guid UserId, string DisplayName, string Handle);
