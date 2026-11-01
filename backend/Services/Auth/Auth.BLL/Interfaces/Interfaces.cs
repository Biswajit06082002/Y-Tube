using Auth.Entities;
using Auth.Entities.Contracts;
using YTube.Common.Results;

namespace Auth.BLL.Interfaces;

// --- Implemented in the BLL (business logic) ---

public interface IAuthService
{
    Task<Result<SignUpResponse>> SignUpAsync(SignUpRequest request, CancellationToken ct = default);
    Task<Result<AuthTokensResponse>> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken ct = default);
    Task<Result<SignUpResponse>> ResendOtpAsync(ResendOtpRequest request, CancellationToken ct = default);
    Task<Result<AuthTokensResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<Result<AuthTokensResponse>> RefreshAsync(RefreshRequest request, CancellationToken ct = default);
    Task<Result<bool>> LogoutAsync(RefreshRequest request, CancellationToken ct = default);
}

// --- Implemented in the DAL (database access) ---

public interface IUserRepository
{
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task UpdateLastLoginAsync(Guid userId, DateTime lastLoginAtUtc, CancellationToken ct = default);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    Task AddAsync(RefreshToken token, CancellationToken ct = default);

    /// <param name="replacedByTokenHash">Set when revoked by rotation; null when revoked by logout.</param>
    Task RevokeAsync(Guid tokenId, string? replacedByTokenHash, CancellationToken ct = default);
    Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>Commits everything the repositories staged, in one database transaction.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

// --- Implemented in the BLL (Services folder) ---

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateAccessToken(User user);

    /// <summary>Returns the raw token (sent to the client once) and its hash (stored in DB).</summary>
    (string RawToken, string TokenHash, DateTime ExpiresAtUtc) CreateRefreshToken();

    string HashRefreshToken(string rawToken);
}

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
