using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using User.BLL.Interfaces;
using User.Entities.Contracts;
using YTube.Common.Responses;
using YTube.Common.Results;

namespace User.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController(IPreferencesService preferences, IProfileService profiles) : ControllerBase
{
    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : null;

    private Guid UserId => CurrentUserId!.Value;
    private string? Email => User.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

    // --- My profile ---

    /// <summary>The signed-in user's profile (created on first call if it doesn't exist yet).</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct) =>
        Ok(ApiResponse<ProfileDto>.Ok(await profiles.GetOrCreateMeAsync(UserId, Email, ct)));

    [HttpPut("me/profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, CancellationToken ct) =>
        (await profiles.UpdateMeAsync(UserId, Email, request, ct)).ToActionResult("Profile saved.");

    [HttpGet("me/following")]
    public async Task<IActionResult> Following(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<ProfileDto>>.Ok(await profiles.GetFollowingAsync(UserId, ct)));

    /// <summary>Just the ids — used by the Feed service to build the Following feed.</summary>
    [HttpGet("me/following/ids")]
    public async Task<IActionResult> FollowingIds(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<Guid>>.Ok(await profiles.GetFollowingIdsAsync(UserId, ct)));

    // --- Anyone's channel ---

    /// <summary>Public channel profile. Optional auth: with a token, IsFollowedByMe/IsMe are filled in.</summary>
    [AllowAnonymous]
    [HttpGet("{id:guid}/profile")]
    public async Task<IActionResult> Profile(Guid id, CancellationToken ct) =>
        await profiles.GetAsync(id, CurrentUserId, ct) is { } profile
            ? Ok(ApiResponse<ProfileDto>.Ok(profile))
            : NotFound(ApiResponse<ProfileDto>.Fail("channel_not_found", "This channel doesn't exist."));

    /// <summary>@mention autocomplete. Public, like channel pages.</summary>
    [AllowAnonymous]
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<ProfileSummaryDto>>.Ok(await profiles.SearchAsync(q ?? "", 6, ct)));

    /// <summary>Resolves @handles to users (called by the Comment service: ?handle=a&amp;handle=b).</summary>
    [AllowAnonymous]
    [HttpGet("by-handles")]
    public async Task<IActionResult> ByHandles([FromQuery(Name = "handle")] string[] handles, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<ProfileSummaryDto>>.Ok(await profiles.GetByHandlesAsync(handles, ct)));

    /// <summary>Follower ids of a creator — called by the Notification service to fan out new-video notifications.</summary>
    [AllowAnonymous]
    [HttpGet("{id:guid}/followers/ids")]
    public async Task<IActionResult> FollowerIds(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<Guid>>.Ok(await profiles.GetFollowerIdsAsync(id, ct)));

    [HttpPut("{id:guid}/follow")]
    public async Task<IActionResult> Follow(Guid id, CancellationToken ct) =>
        (await profiles.SetFollowAsync(UserId, id, follow: true, ct)).ToActionResult();

    [HttpDelete("{id:guid}/follow")]
    public async Task<IActionResult> Unfollow(Guid id, CancellationToken ct) =>
        (await profiles.SetFollowAsync(UserId, id, follow: false, ct)).ToActionResult();

    // --- Preferences ---

    [HttpGet("me/preferences")]
    public async Task<IActionResult> GetPreferences(CancellationToken ct) =>
        Ok(ApiResponse<PreferencesDto>.Ok(await preferences.GetAsync(UserId, ct)));

    [HttpPut("me/preferences")]
    public async Task<IActionResult> UpdatePreferences(UpdatePreferencesRequest request, CancellationToken ct) =>
        (await preferences.UpdateAsync(UserId, request, ct)).ToActionResult();
}
