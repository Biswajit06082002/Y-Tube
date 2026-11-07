using System.IdentityModel.Tokens.Jwt;
using Comment.BLL.Interfaces;
using Comment.Entities.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YTube.Common.Results;

namespace Comment.Api.Controllers;

/// <summary>Reading comments is public; writing needs a login.</summary>
[ApiController]
[Route("api/comments")]
[Authorize]
public sealed class CommentsController(ICommentService comments) : ControllerBase
{
    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : null;

    private Guid UserId => CurrentUserId!.Value;

    /// <summary>Top-level comments of a video, newest first. Pass nextCursor back as ?cursor= for more.</summary>
    [AllowAnonymous]
    [HttpGet("video/{videoId}")]
    public async Task<IActionResult> ForVideo(string videoId, [FromQuery] long? cursor, CancellationToken ct) =>
        (await comments.GetCommentsAsync(videoId, cursor, CurrentUserId, ct)).ToActionResult();

    [AllowAnonymous]
    [HttpGet("{id:long}/replies")]
    public async Task<IActionResult> Replies(long id, [FromQuery] long? cursor, CancellationToken ct) =>
        (await comments.GetRepliesAsync(id, cursor, CurrentUserId, ct)).ToActionResult();

    [HttpPost("video/{videoId}")]
    public async Task<IActionResult> Post(string videoId, PostCommentRequest request, CancellationToken ct) =>
        (await comments.PostAsync(videoId, UserId, request, ct)).ToActionResult();

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Edit(long id, EditCommentRequest request, CancellationToken ct) =>
        (await comments.EditAsync(id, UserId, request, ct)).ToActionResult();

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct) =>
        (await comments.DeleteAsync(id, UserId, ct)).ToActionResult();
}
