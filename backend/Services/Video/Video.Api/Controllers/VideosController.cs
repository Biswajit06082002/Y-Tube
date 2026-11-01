using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Video.Api.Catalog;
using YTube.Common.Responses;

namespace Video.Api.Controllers;

/// <summary>
/// Reads are public (anyone can watch). Anything that acts as a user needs a login.
/// Public endpoints still read the token when one is sent, so they can personalise
/// the answer (e.g. "likedByMe") — this is "optional authentication".
/// </summary>
[ApiController]
[Route("api/videos")]
public sealed class VideosController(VideoCatalog catalog) : ControllerBase
{
    private const int MaxPageSize = 50;

    private string? UserId => User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

    [HttpGet("ping")]
    public IActionResult Ping() => Ok(ApiResponse<string>.Ok("Video service is up"));

    [HttpGet]
    public IActionResult List(int page = 1, int pageSize = 12, string? category = null)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var (items, total) = catalog.List(page, pageSize, category, UserId);
        return Ok(ApiResponse<PagedResult<VideoDto>>.Ok(new PagedResult<VideoDto>(items, page, pageSize, total)));
    }

    [HttpGet("categories")]
    public IActionResult Categories() => Ok(ApiResponse<IReadOnlyList<string>>.Ok(catalog.Categories));

    [HttpGet("{id}")]
    public IActionResult Get(string id) =>
        catalog.Get(id, UserId) is { } video
            ? Ok(ApiResponse<VideoDto>.Ok(video))
            : NotFound(ApiResponse<VideoDto>.Fail("video_not_found", "This video isn't available."));

    [Authorize]
    [HttpGet("liked")]
    public IActionResult Liked() => Ok(ApiResponse<IReadOnlyList<VideoDto>>.Ok(catalog.LikedBy(UserId!)));

    [Authorize]
    [HttpPut("{id}/like")]
    public IActionResult Like(string id) => LikeResult(catalog.SetLike(id, UserId!, liked: true));

    [Authorize]
    [HttpDelete("{id}/like")]
    public IActionResult Unlike(string id) => LikeResult(catalog.SetLike(id, UserId!, liked: false));

    private IActionResult LikeResult(VideoDto? video) =>
        video is not null
            ? Ok(ApiResponse<VideoDto>.Ok(video))
            : NotFound(ApiResponse<VideoDto>.Fail("video_not_found", "This video isn't available."));
}
