using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Video.BLL.Interfaces;
using Video.Entities.Contracts;
using YTube.Common.Responses;

namespace Video.Api.Controllers;

/// <summary>
/// Reads are public (anyone can watch). Anything that acts as a user needs a login.
/// Public endpoints still read the token when one is sent, so they can personalise
/// the answer (e.g. "likedByMe") — this is "optional authentication".
/// </summary>
[ApiController]
[Route("api/videos")]
public sealed class VideosController(IVideoService videos) : ControllerBase
{
    private string? UserId => User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

    [HttpGet("ping")]
    public IActionResult Ping() => Ok(ApiResponse<string>.Ok("Video service is up"));

    [HttpGet]
    public async Task<IActionResult> List(int page = 1, int pageSize = 12, string? category = null, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<VideoDto>>.Ok(await videos.ListAsync(page, pageSize, category, UserId, ct)));

    [HttpGet("categories")]
    public async Task<IActionResult> Categories(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<string>>.Ok(await videos.CategoriesAsync(ct)));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct) =>
        VideoResult(await videos.GetAsync(id, UserId, ct));

    [Authorize]
    [HttpGet("liked")]
    public async Task<IActionResult> Liked(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<VideoDto>>.Ok(await videos.LikedByAsync(UserId!, ct)));

    [Authorize]
    [HttpPut("{id}/like")]
    public async Task<IActionResult> Like(string id, CancellationToken ct) =>
        VideoResult(await videos.SetLikeAsync(id, UserId!, liked: true, ct));

    [Authorize]
    [HttpDelete("{id}/like")]
    public async Task<IActionResult> Unlike(string id, CancellationToken ct) =>
        VideoResult(await videos.SetLikeAsync(id, UserId!, liked: false, ct));

    private IActionResult VideoResult(VideoDto? video) =>
        video is not null
            ? Ok(ApiResponse<VideoDto>.Ok(video))
            : NotFound(ApiResponse<VideoDto>.Fail("video_not_found", "This video isn't available."));
}
