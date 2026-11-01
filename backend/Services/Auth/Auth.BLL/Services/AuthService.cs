using System.Net.Mail;
using System.Security.Cryptography;
using Auth.BLL.Interfaces;
using Auth.Entities;
using Auth.Entities.Contracts;
using Microsoft.Extensions.Logging;
using YTube.Common.Caching;
using YTube.Common.Results;

namespace Auth.BLL.Services;

public sealed class AuthService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    ITokenService tokenService,
    IEmailSender emailSender,
    ICacheService cache,
    ILogger<AuthService> logger) : IAuthService
{
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
    private const int MaxOtpAttempts = 5;
    private const int MaxResends = 3;
    private const int MinPasswordLength = 8;
    private static readonly TimeSpan RotationGracePeriod = TimeSpan.FromSeconds(30);

    private static string PendingKey(string signupId) => $"auth:signup:{signupId}";

    public async Task<Result<SignUpResponse>> SignUpAsync(SignUpRequest request, CancellationToken ct = default)
    {
        var email = NormalizeEmail(request.Email);
        if (!IsValidEmail(email))
            return Error.Validation("invalid_email", "Please enter a valid email address.");
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < MinPasswordLength)
            return Error.Validation("weak_password", $"Password must be at least {MinPasswordLength} characters.");

        if (await users.EmailExistsAsync(email, ct))
            return Error.Conflict("email_taken", "An account with this email already exists.");

        var otp = GenerateOtp();
        var pending = new PendingSignup
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            OtpHash = BCrypt.Net.BCrypt.HashPassword(otp)
        };

        // Random id instead of a hash of the email: it can't be guessed from the email address.
        var signupId = Guid.NewGuid().ToString("N");
        await cache.SetAsync(PendingKey(signupId), pending, OtpLifetime, ct);
        await SendOtpEmailAsync(email, otp, ct);

        return new SignUpResponse(signupId, (int)OtpLifetime.TotalSeconds);
    }

    public async Task<Result<AuthTokensResponse>> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken ct = default)
    {
        var key = PendingKey(request.SignupId);
        var pending = await cache.GetAsync<PendingSignup>(key, ct);
        if (pending is null)
            return Error.Gone("otp_expired", "This code has expired. Please sign up again.");

        // BCrypt hashes are salted, so hashing again and comparing strings never matches — use Verify.
        if (!BCrypt.Net.BCrypt.Verify(request.Otp?.Trim() ?? string.Empty, pending.OtpHash))
        {
            pending.FailedAttempts++;
            if (pending.FailedAttempts >= MaxOtpAttempts)
            {
                await cache.RemoveAsync(key, ct);
                return Error.TooManyRequests("otp_locked", "Too many wrong attempts. Please sign up again.");
            }

            await cache.SetAsync(key, pending, await RemainingTtlAsync(key, ct), ct);
            return Error.Validation("otp_invalid", $"Incorrect code. {MaxOtpAttempts - pending.FailedAttempts} attempts left.");
        }

        // Re-check: another sign-up for the same email may have completed meanwhile.
        if (await users.EmailExistsAsync(pending.Email, ct))
        {
            await cache.RemoveAsync(key, ct);
            return Error.Conflict("email_taken", "An account with this email already exists.");
        }

        var user = new User { Email = pending.Email, PasswordHash = pending.PasswordHash, EmailVerified = true };
        await users.AddAsync(user, ct);
        var tokens = await IssueTokensAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await cache.RemoveAsync(key, ct);

        logger.LogInformation("User {UserId} signed up", user.Id);
        return tokens;
    }

    public async Task<Result<SignUpResponse>> ResendOtpAsync(ResendOtpRequest request, CancellationToken ct = default)
    {
        var key = PendingKey(request.SignupId);
        var pending = await cache.GetAsync<PendingSignup>(key, ct);
        if (pending is null)
            return Error.Gone("otp_expired", "This sign-up has expired. Please sign up again.");
        if (pending.ResendCount >= MaxResends)
            return Error.TooManyRequests("resend_limit", "You've requested too many codes. Please sign up again.");

        var otp = GenerateOtp();
        pending.OtpHash = BCrypt.Net.BCrypt.HashPassword(otp);
        pending.FailedAttempts = 0;
        pending.ResendCount++;

        await cache.SetAsync(key, pending, OtpLifetime, ct);
        await SendOtpEmailAsync(pending.Email, otp, ct);

        return new SignUpResponse(request.SignupId, (int)OtpLifetime.TotalSeconds);
    }

    public async Task<Result<AuthTokensResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await users.GetByEmailAsync(NormalizeEmail(request.Email), ct);

        // Same message for "no such user" and "wrong password" so attackers can't probe which emails exist.
        if (user is null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(request.Password ?? string.Empty, user.PasswordHash))
            return Error.Unauthorized("invalid_credentials", "Email or password is incorrect.");

        await users.UpdateLastLoginAsync(user.Id, DateTime.UtcNow, ct);
        var tokens = await IssueTokensAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return tokens;
    }

    public async Task<Result<AuthTokensResponse>> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var existing = await refreshTokens.GetByHashAsync(tokenService.HashRefreshToken(request.RefreshToken), ct);
        var user = existing is null ? null : await users.GetByIdAsync(existing.UserId, ct);
        if (existing is null || user is null)
            return Error.Unauthorized("invalid_refresh_token", "Session expired. Please log in again.");

        if (!existing.IsActive)
        {
            // Grace window: two tabs can refresh with the same cookie at the same moment. The second
            // one arrives with a token that was rotated a second ago — that's a race, not theft.
            if (existing is { RevokedAtUtc: { } revokedAt, ReplacedByTokenHash: not null }
                && DateTime.UtcNow - revokedAt < RotationGracePeriod
                && DateTime.UtcNow < existing.ExpiresAtUtc)
            {
                var raceTokens = await IssueTokensAsync(user, ct);
                await unitOfWork.SaveChangesAsync(ct);
                return raceTokens;
            }

            // Otherwise a revoked token being reused means it was probably stolen: kill every session of this user.
            if (existing.RevokedAtUtc is not null)
            {
                logger.LogWarning("Refresh token reuse detected for user {UserId}", existing.UserId);
                await refreshTokens.RevokeAllForUserAsync(existing.UserId, ct);
                await unitOfWork.SaveChangesAsync(ct);
            }
            return Error.Unauthorized("invalid_refresh_token", "Session expired. Please log in again.");
        }

        // Rotation: every refresh hands out a new refresh token and retires the old one.
        var tokens = await IssueTokensAsync(user, ct);
        await refreshTokens.RevokeAsync(existing.Id, tokenService.HashRefreshToken(tokens.RefreshToken), ct);
        await unitOfWork.SaveChangesAsync(ct);
        return tokens;
    }

    public async Task<Result<bool>> LogoutAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var existing = await refreshTokens.GetByHashAsync(tokenService.HashRefreshToken(request.RefreshToken), ct);
        if (existing is { IsActive: true })
        {
            await refreshTokens.RevokeAsync(existing.Id, replacedByTokenHash: null, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        return true;
    }

    private async Task<AuthTokensResponse> IssueTokensAsync(User user, CancellationToken ct)
    {
        var (accessToken, accessExpires) = tokenService.CreateAccessToken(user);
        var (rawRefresh, refreshHash, refreshExpires) = tokenService.CreateRefreshToken();

        await refreshTokens.AddAsync(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAtUtc = refreshExpires
        }, ct);

        return new AuthTokensResponse(accessToken, accessExpires, rawRefresh, refreshExpires, new UserSummary(user.Id, user.Email));
    }

    private Task SendOtpEmailAsync(string email, string otp, CancellationToken ct) =>
        emailSender.SendAsync(
            email,
            "Your YTube verification code",
            $"<h2>Welcome to YTube!</h2><p>Your verification code is:</p><h1>{otp}</h1><p>It expires in {OtpLifetime.TotalMinutes} minutes.</p>",
            ct);

    private async Task<TimeSpan> RemainingTtlAsync(string key, CancellationToken ct) =>
        await cache.GetTimeToLiveAsync(key, ct) ?? OtpLifetime;

    // Cryptographically secure, unlike System.Random.
    private static string GenerateOtp() => RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

    private static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    private static bool IsValidEmail(string email) =>
        email.Length <= 256 && MailAddress.TryCreate(email, out var parsed) && parsed.Address == email;
}
