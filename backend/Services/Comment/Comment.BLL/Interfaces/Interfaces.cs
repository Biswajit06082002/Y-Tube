using Comment.Entities;
using Comment.Entities.Contracts;
using YTube.Common.Results;
using YTube.Contracts.Events;

namespace Comment.BLL.Interfaces;

// --- Implemented in the BLL (business logic) ---

public interface ICommentService
{
    /// <summary>Top-level comments, newest first. Cursor = the last id of the previous page.</summary>
    Task<Result<CommentPage>> GetCommentsAsync(string videoId, long? cursor, Guid? viewerId, CancellationToken ct = default);

    /// <summary>Replies of one comment, oldest first (a conversation reads top to bottom).</summary>
    Task<Result<CommentPage>> GetRepliesAsync(long commentId, long? cursor, Guid? viewerId, CancellationToken ct = default);

    Task<Result<CommentDto>> PostAsync(string videoId, Guid authorId, PostCommentRequest request, CancellationToken ct = default);
    Task<Result<CommentDto>> EditAsync(long commentId, Guid userId, EditCommentRequest request, CancellationToken ct = default);

    /// <summary>The author or the video's creator may delete. Deleting a comment removes its replies too.</summary>
    Task<Result<bool>> DeleteAsync(long commentId, Guid userId, CancellationToken ct = default);
}

/// <summary>Keeps the copied author name/@handle on comments in sync ("user.profile.updated").</summary>
public interface IAuthorSyncHandler
{
    Task HandleAsync(UserProfileUpdated e, CancellationToken ct = default);
}

// --- Implemented in the DAL (database access and other services) ---

public interface ICommentRepository
{
    Task<CommentItem?> GetAsync(long id, CancellationToken ct = default);

    /// <summary>Top-level comments with Id &lt; beforeId (all when null), newest first.</summary>
    Task<IReadOnlyList<CommentItem>> GetTopLevelAsync(string videoId, long? beforeId, int take, CancellationToken ct = default);

    /// <summary>Replies with Id &gt; afterId (all when null), oldest first.</summary>
    Task<IReadOnlyList<CommentItem>> GetRepliesAsync(long parentId, long? afterId, int take, CancellationToken ct = default);

    /// <summary>Comments + replies on a video.</summary>
    Task<int> CountAsync(string videoId, CancellationToken ct = default);

    /// <summary>Saves the comment (and bumps the parent's ReplyCount) in one transaction. Fills in Id.</summary>
    Task AddAsync(CommentItem comment, CancellationToken ct = default);

    Task UpdateTextAsync(long id, string text, IReadOnlyList<CommentMention> mentions, DateTime editedAtUtc, CancellationToken ct = default);

    /// <summary>Deletes the comment; a top-level one takes its replies with it, a reply lowers its parent's count.</summary>
    Task DeleteAsync(CommentItem comment, CancellationToken ct = default);

    Task RenameAuthorAsync(Guid authorId, string name, string handle, CancellationToken ct = default);
}

/// <summary>Video service: does the video exist, and who made it?</summary>
public interface IVideoClient
{
    /// <summary>Null when the video doesn't exist. Throws when the Video service can't be reached.</summary>
    Task<VideoInfo?> GetAsync(string videoId, CancellationToken ct = default);
}

/// <summary>User service: who is writing, and which users do @handles point at?</summary>
public interface IUserClient
{
    /// <summary>The signed-in user's name and @handle (the caller's token is forwarded).</summary>
    Task<AuthorInfo?> GetMeAsync(CancellationToken ct = default);

    Task<IReadOnlyList<AuthorInfo>> GetByHandlesAsync(IReadOnlyCollection<string> handles, CancellationToken ct = default);
}
