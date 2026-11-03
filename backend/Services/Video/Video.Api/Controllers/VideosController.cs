using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Video.Api.Models;
using Video.BLL.Interfaces;
using Video.BLL.Services;
using Video.Entities.Contracts;
using YTube.Common.Responses;
using YTube.Common.Results;

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
    // A little headroom over the 2 GB file limit for the other form fields.
    private const long MaxRequestBytes = UploadRules.MaxVideoBytes + UploadRules.MaxThumbnailBytes + 1024 * 1024;

    private string? UserId => User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

    [HttpGet("ping")]
    public IActionResult Ping() => Ok(ApiResponse<string>.Ok("Video service is up"));

    [HttpGet]
    public async Task<IActionResult> List(int page = 1, int pageSize = 12, string? category = null, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<VideoDto>>.Ok(await videos.ListAsync(page, pageSize, category, UserId, ct)));

    [HttpGet("categories")]
    public IActionResult Categories() => Ok(ApiResponse<IReadOnlyList<string>>.Ok(videos.Categories()));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct) =>
        VideoResult(await videos.GetAsync(id, UserId, ct));

    [Authorize]
    [HttpGet("liked")]
    public async Task<IActionResult> Liked(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<VideoDto>>.Ok(await videos.LikedByAsync(UserId!, ct)));

    /// <summary>The signed-in user's uploads, including ones still processing.</summary>
    [Authorize]
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<VideoDto>>.Ok(await videos.ListMineAsync(Guid.Parse(UserId!), ct)));

    /// <summary>
    /// Multipart upload: the video file, an optional thumbnail, and the details.
    /// Returns immediately with status "Queued" — transcoding happens in the background.
    /// </summary>
    [Authorize]
    [HttpPost]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<IActionResult> Upload([FromForm] UploadVideoForm form, CancellationToken ct)
    {
        var email = User.FindFirst(JwtRegisteredClaimNames.Email)?.Value ?? "creator";

        await using var videoStream = form.Video?.OpenReadStream();
        await using var thumbnailStream = form.Thumbnail?.OpenReadStream();

        var command = new UploadVideoCommand(
            OwnerId: Guid.Parse(UserId!),
            OwnerName: email.Split('@')[0],
            Title: form.Title ?? string.Empty,
            Description: form.Description,
            Category: form.Category ?? string.Empty,
            Tags: (form.Tags ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries),
            Video: form.Video is null ? null : new UploadedFile(videoStream!, form.Video.FileName, form.Video.Length),
            Thumbnail: form.Thumbnail is null ? null : new UploadedFile(thumbnailStream!, form.Thumbnail.FileName, form.Thumbnail.Length));

        return (await videos.UploadAsync(command, ct)).ToActionResult("Upload complete — your video is being processed.");
    }

    // --- Channels ---

    [HttpGet("channel/{ownerId:guid}")]
    public async Task<IActionResult> Channel(Guid ownerId, int page = 1, int pageSize = 12, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<VideoDto>>.Ok(await videos.ListByChannelAsync(ownerId, page, pageSize, UserId, ct)));

    [HttpGet("channel/{ownerId:guid}/stats")]
    public async Task<IActionResult> ChannelStats(Guid ownerId, CancellationToken ct) =>
        Ok(ApiResponse<ChannelStatsDto>.Ok(await videos.ChannelStatsAsync(ownerId, ct)));

    /// <summary>Public videos of several creators — the Feed service uses this for the Following feed.</summary>
    [HttpGet("by-owners")]
    public async Task<IActionResult> ByOwners([FromQuery] string ownerIds = "", int page = 1, int pageSize = 12, CancellationToken ct = default)
    {
        var ids = ownerIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .Distinct()
            .Take(500)
            .ToList();
        return Ok(ApiResponse<PagedResult<VideoDto>>.Ok(await videos.ListByOwnersAsync(ids, page, pageSize, UserId, ct)));
    }

    // --- Views ---

    /// <summary>
    /// Reported by the player after a few seconds of real playback. Anonymous viewers count too;
    /// they're identified by a hash of IP + browser so the same person isn't counted twice.
    /// </summary>
    [HttpPost("{id}/views")]
    public async Task<IActionResult> RecordView(string id, [FromServices] IViewService views, CancellationToken ct)
    {
        var viewer = UserId is { } uid ? $"u:{uid}" : $"a:{AnonymousViewerHash()}";
        return (await views.RecordViewAsync(id, viewer, ct)).ToActionResult();
    }

    private string AnonymousViewerHash()
    {
        // RemoteIpAddress is the real client thanks to UseForwardedHeaders (the gateway adds X-Forwarded-For).
        var raw = $"{HttpContext.Connection.RemoteIpAddress}|{Request.Headers.UserAgent}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..16];
    }

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
