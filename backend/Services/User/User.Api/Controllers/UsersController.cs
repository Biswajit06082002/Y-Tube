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
public sealed class UsersController(IPreferencesService preferences) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    [HttpGet("me")]
    public IActionResult Me() =>
        Ok(ApiResponse<object>.Ok(new
        {
            Id = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
            Email = User.FindFirst(JwtRegisteredClaimNames.Email)?.Value
        }));

    [HttpGet("me/preferences")]
    public async Task<IActionResult> GetPreferences(CancellationToken ct) =>
        Ok(ApiResponse<PreferencesDto>.Ok(await preferences.GetAsync(UserId, ct)));

    [HttpPut("me/preferences")]
    public async Task<IActionResult> UpdatePreferences(UpdatePreferencesRequest request, CancellationToken ct) =>
        (await preferences.UpdateAsync(UserId, request, ct)).ToActionResult();
}
