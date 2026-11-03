using Feed.BLL.Interfaces;
using Feed.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YTube.Common.Responses;

namespace Feed.Api.Controllers;

[ApiController]
[Route("api/feed")]
public sealed class FeedController(IFeedService feed) : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(ApiResponse<string>.Ok("Feed service is up"));

    /// <summary>Home feed — public, like a video site's home page.</summary>
    [HttpGet]
    public async Task<IActionResult> Home(int page = 1, int pageSize = 12, string? category = null, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<FeedVideo>>.Ok(await feed.HomeAsync(page, pageSize, category, ct)));

    [HttpGet("categories")]
    public async Task<IActionResult> Categories(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<string>>.Ok(await feed.CategoriesAsync(ct)));

    /// <summary>Videos from followed creators. Needs a login.</summary>
    [Authorize]
    [HttpGet("subscriptions")]
    public async Task<IActionResult> Subscriptions(int page = 1, int pageSize = 12, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<FeedVideo>>.Ok(await feed.SubscriptionsAsync(page, pageSize, ct)));
}
