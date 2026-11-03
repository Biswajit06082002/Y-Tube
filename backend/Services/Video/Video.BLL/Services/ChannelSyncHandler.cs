using Video.BLL.Interfaces;
using YTube.Contracts.Events;

namespace Video.BLL.Services;

/// <summary>
/// Each video stores its creator's name (denormalised) so feeds don't need to ask the User service
/// for every card. When the creator renames themselves, the User service publishes an event and we
/// update our copies — the names converge a moment later ("eventual consistency").
/// </summary>
public sealed class ChannelSyncHandler(IVideoRepository repository) : IChannelSyncHandler
{
    public Task HandleAsync(UserProfileUpdated e, CancellationToken ct = default) =>
        repository.UpdateChannelNameAsync(e.UserId, e.DisplayName, ct);
}
