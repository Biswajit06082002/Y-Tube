using Feed.Entities;
using YTube.Common.Responses;

namespace Feed.BLL.Interfaces;

// --- Implemented in the BLL (business logic) ---

public interface IFeedService
{
    Task<PagedResult<FeedVideo>> HomeAsync(int page, int pageSize, string? category, CancellationToken ct = default);
    Task<IReadOnlyList<string>> CategoriesAsync(CancellationToken ct = default);
    /// <summary>Videos from creators the signed-in user follows (identified by the forwarded token).</summary>
    Task<PagedResult<FeedVideo>> SubscriptionsAsync(int page, int pageSize, CancellationToken ct = default);
}

// --- Implemented in the DAL (data source: the Video service over HTTP) ---

public interface IVideoCatalogClient
{
    Task<PagedResult<FeedVideo>> GetVideosAsync(int page, int pageSize, string? category, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default);
    Task<PagedResult<FeedVideo>> GetVideosByOwnersAsync(IReadOnlyCollection<Guid> ownerIds, int page, int pageSize, CancellationToken ct = default);
}

// --- Implemented in the DAL (data source: the User service over HTTP) ---

public interface IUserFollowsClient
{
    /// <summary>Ids of the creators the current user follows (the user comes from the forwarded token).</summary>
    Task<IReadOnlyList<Guid>> GetFollowingIdsAsync(CancellationToken ct = default);
}
