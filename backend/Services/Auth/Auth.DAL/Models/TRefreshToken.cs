namespace Auth.DAL.Models;

/// <summary>Row of the RefreshTokens table (only the SHA-256 hash of the token is stored).</summary>
public class TRefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public TUser? User { get; set; }
}
