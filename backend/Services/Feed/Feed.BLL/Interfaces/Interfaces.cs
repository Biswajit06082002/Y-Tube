using Feed.Entities;
using YTube.Common.Responses;

namespace Feed.BLL.Interfaces;

// --- Implemented in the BLL (business logic) ---

public interface IFeedService
{
    Task<PagedResult<FeedVideo>> HomeAsync(int page, int pageSize, string? category, CancellationToken ct = default);
    Task<IReadOnlyList<string>> CategoriesAsync(CancellationToken ct = default);
    Task<PagedResult<FeedVideo>> SubscriptionsAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);
}

// --- Implemented in the DAL (data source: the Video service over HTTP) ---

public interface IVideoCatalogClient
{
    Task<PagedResult<FeedVideo>> GetVideosAsync(int page, int pageSize, string? category, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default);
}
