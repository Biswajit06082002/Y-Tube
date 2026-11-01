using Auth.Application;
using Auth.Application.Contracts;
using Microsoft.AspNetCore.Mvc;
using YTube.Common.Results;

namespace Auth.Api.Controllers;

/// <summary>What the browser gets back: the access token only. The refresh token travels in a cookie.</summary>
public sealed record SessionResponse(string AccessToken, DateTime AccessTokenExpiresAtUtc, UserSummary User);

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService, IWebHostEnvironment env) : ControllerBase
{
    private const string RefreshCookie = "ytube_rt";

    /// <summary>Step 1 of sign-up: stores the pending account in Redis and emails a 6-digit code.</summary>
    [HttpPost("signup")]
    public async Task<IActionResult> SignUp(SignUpRequest request, CancellationToken ct) =>
        (await authService.SignUpAsync(request, ct)).ToActionResult("Verification code sent.");

    /// <summary>Step 2 of sign-up: checks the code, creates the user and logs them in.</summary>
    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp(VerifyOtpRequest request, CancellationToken ct) =>
        StartSession(await authService.VerifyOtpAsync(request, ct), "Sign-up complete.");

    [HttpPost("resend-otp")]
    public async Task<IActionResult> ResendOtp(ResendOtpRequest request, CancellationToken ct) =>
        (await authService.ResendOtpAsync(request, ct)).ToActionResult("A new code was sent.");

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct) =>
        StartSession(await authService.LoginAsync(request, ct));

    /// <summary>
    /// New access token from the refresh-token cookie. Called when the access token expires
    /// and when the app opens (to restore the session without asking for a password).
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var refreshToken = Request.Cookies[RefreshCookie];
        if (string.IsNullOrEmpty(refreshToken))
            return Result<SessionResponse>.Failure(Error.Unauthorized("no_session", "Not signed in.")).ToActionResult();

        var result = await authService.RefreshAsync(new RefreshRequest(refreshToken), ct);
        if (!result.IsSuccess) ClearRefreshCookie();
        return StartSession(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var refreshToken = Request.Cookies[RefreshCookie];
        if (!string.IsNullOrEmpty(refreshToken))
            await authService.LogoutAsync(new RefreshRequest(refreshToken), ct);

        ClearRefreshCookie();
        return Result<bool>.Success(true).ToActionResult();
    }

    private IActionResult StartSession(Result<AuthTokensResponse> result, string? message = null)
    {
        if (!result.IsSuccess)
            return Result<SessionResponse>.Failure(result.Error!).ToActionResult();

        var tokens = result.Value!;
        Response.Cookies.Append(RefreshCookie, tokens.RefreshToken, CookieOptions(tokens.RefreshTokenExpiresAtUtc));
        return Result<SessionResponse>
            .Success(new SessionResponse(tokens.AccessToken, tokens.AccessTokenExpiresAtUtc, tokens.User))
            .ToActionResult(message);
    }

    private void ClearRefreshCookie() => Response.Cookies.Delete(RefreshCookie, CookieOptions(expires: null));

    /// <summary>
    /// HttpOnly: JavaScript can't read it, so an XSS bug can't steal it.
    /// SameSite=Strict: the browser won't attach it to requests started by other sites (CSRF protection).
    /// Path=/api/auth: it's only sent to the auth endpoints, not with every API call.
    /// Secure: HTTPS only — relaxed in Development because everything runs on plain http://localhost.
    /// </summary>
    private CookieOptions CookieOptions(DateTime? expires) => new()
    {
        HttpOnly = true,
        Secure = !env.IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth",
        Expires = expires
    };
}
