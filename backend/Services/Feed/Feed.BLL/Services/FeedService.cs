using Feed.BLL.Interfaces;
using Feed.Entities;
using YTube.Common.Responses;

namespace Feed.BLL.Services;

/// <summary>
/// Builds feeds. Today the home feed is the Video service's list as-is; this is the place where
/// ranking, personalisation and (later) ML recommendations will live.
/// </summary>
public sealed class FeedService(IVideoCatalogClient videos, IUserFollowsClient follows) : IFeedService
{
    private const int MaxPageSize = 50;

    public Task<PagedResult<FeedVideo>> HomeAsync(int page, int pageSize, string? category, CancellationToken ct = default) =>
        videos.GetVideosAsync(Math.Max(page, 1), Math.Clamp(pageSize, 1, MaxPageSize), category, ct);

    public Task<IReadOnlyList<string>> CategoriesAsync(CancellationToken ct = default) => videos.GetCategoriesAsync(ct);

    /// <summary>
    /// "Fan-out on read": when you open Following, we ask User "who do you follow?" and then Video
    /// "newest videos from these creators". Cheap to write (an upload touches nothing else) but each
    /// read does the work. Big platforms often switch to "fan-out on write" for most users — pushing
    /// each new video into every follower's pre-built feed — and keep fan-out on read for celebrities
    /// with millions of followers.
    /// </summary>
    public async Task<PagedResult<FeedVideo>> SubscriptionsAsync(int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var following = await follows.GetFollowingIdsAsync(ct);
        return following.Count == 0
            ? PagedResult<FeedVideo>.Empty(page, pageSize)
            : await videos.GetVideosByOwnersAsync(following, page, pageSize, ct);
    }
}
