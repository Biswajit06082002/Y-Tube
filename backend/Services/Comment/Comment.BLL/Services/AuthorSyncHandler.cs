using Comment.BLL.Interfaces;
using YTube.Contracts.Events;

namespace Comment.BLL.Services;

/// <summary>A renamed user: update the name/@handle copied onto their comments (eventual consistency).</summary>
public sealed class AuthorSyncHandler(ICommentRepository comments) : IAuthorSyncHandler
{
    public Task HandleAsync(UserProfileUpdated e, CancellationToken ct = default) =>
        comments.RenameAuthorAsync(e.UserId, e.DisplayName, e.Handle, ct);
}
