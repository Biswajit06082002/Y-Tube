namespace Auth.DAL.Models;

/// <summary>Row of the Users table. Only the DAL sees this; the BLL works with Auth.Entities.User.</summary>
public class TUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }

    public List<TRefreshToken> RefreshTokens { get; set; } = [];
}
