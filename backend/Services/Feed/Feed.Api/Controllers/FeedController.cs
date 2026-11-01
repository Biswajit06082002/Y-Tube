using Feed.Api.Clients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YTube.Common.Responses;

namespace Feed.Api.Controllers;

[ApiController]
[Route("api/feed")]
public sealed class FeedController(VideoServiceClient videos) : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(ApiResponse<string>.Ok("Feed service is up"));

    /// <summary>
    /// Home feed — public, like YouTube's home page. Everyone gets the same feed for now;
    /// once we know the user (token present) this is where personalised ranking / ML plugs in.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Home(int page = 1, int pageSize = 12, string? category = null, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var result = await videos.GetVideosAsync(page, pageSize, category, ct);
        return Ok(ApiResponse<PagedResult<FeedVideo>>.Ok(result));
    }

    [HttpGet("categories")]
    public async Task<IActionResult> Categories(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<string>>.Ok(await videos.GetCategoriesAsync(ct)));

    /// <summary>Videos from subscribed channels. Needs a login; empty until the User service tracks subscriptions.</summary>
    [Authorize]
    [HttpGet("subscriptions")]
    public IActionResult Subscriptions(int page = 1, int pageSize = 12) =>
        Ok(ApiResponse<PagedResult<FeedVideo>>.Ok(PagedResult<FeedVideo>.Empty(page, pageSize)));
}
