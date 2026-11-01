namespace Auth.Application.Domain;

/// <summary>
/// Long-lived token used to get new short-lived access tokens. Only its SHA-256 hash is stored,
/// so a database leak doesn't hand out usable tokens.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>Hash of the token that replaced this one (rotation chain).</summary>
    public string? ReplacedByTokenHash { get; set; }

    public User? User { get; set; }

    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;
}
