using Feed.BLL.Interfaces;
using Feed.Entities;
using YTube.Common.Responses;

namespace Feed.BLL.Services;

/// <summary>
/// Builds feeds. Today the home feed is the Video service's list as-is; this is the place where
/// ranking, personalisation and (later) ML recommendations will live.
/// </summary>
public sealed class FeedService(IVideoCatalogClient videos) : IFeedService
{
    private const int MaxPageSize = 50;

    public Task<PagedResult<FeedVideo>> HomeAsync(int page, int pageSize, string? category, CancellationToken ct = default) =>
        videos.GetVideosAsync(Math.Max(page, 1), Math.Clamp(pageSize, 1, MaxPageSize), category, ct);

    public Task<IReadOnlyList<string>> CategoriesAsync(CancellationToken ct = default) => videos.GetCategoriesAsync(ct);

    /// <summary>Empty until the User service tracks who follows whom.</summary>
    public Task<PagedResult<FeedVideo>> SubscriptionsAsync(Guid userId, int page, int pageSize, CancellationToken ct = default) =>
        Task.FromResult(PagedResult<FeedVideo>.Empty(Math.Max(page, 1), Math.Clamp(pageSize, 1, MaxPageSize)));
}
