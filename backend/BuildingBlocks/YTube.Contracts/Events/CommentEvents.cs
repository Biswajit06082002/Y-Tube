namespace YTube.Contracts.Events;

public static class CommentRoutingKeys
{
    public const string UserMentioned = "comment.user-mentioned";
    public const string CommentCreated = "comment.created";
}

/// <summary>
/// Published by the Comment service for every user tagged with @handle in a new (or edited) comment.
/// The Notification service turns it into a live "you were mentioned".
/// </summary>
public sealed record UserMentioned(
    long CommentId,
    string VideoId,
    string VideoTitle,
    Guid MentionedUserId,
    Guid AuthorId,
    string AuthorName,
    string Snippet,
    DateTime MentionedAtUtc);

/// <summary>
/// Published by the Comment service for every new comment or reply. The Notification service tells the
/// video's creator ("new comment") or the parent comment's author ("new reply"), skipping anyone in
/// MentionedUserIds — they already get a "mentioned you" notification for the same comment.
/// </summary>
public sealed record CommentCreated(
    long CommentId,
    long? ParentId,
    string VideoId,
    string VideoTitle,
    Guid? VideoOwnerId,
    Guid? ParentAuthorId,
    Guid AuthorId,
    string AuthorName,
    string Snippet,
    IReadOnlyList<Guid> MentionedUserIds,
    DateTime CreatedAtUtc);
