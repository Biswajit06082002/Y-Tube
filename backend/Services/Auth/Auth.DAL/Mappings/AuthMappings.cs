using Auth.DAL.Models;
using Auth.Entities;

namespace Auth.DAL.Mappings;

/// <summary>Converts between table models (DAL) and business entities (BLL). Kept in one place on purpose.</summary>
internal static class AuthMappings
{
    public static User ToEntity(this TUser m) => new()
    {
        Id = m.Id,
        Email = m.Email,
        PasswordHash = m.PasswordHash,
        EmailVerified = m.EmailVerified,
        IsActive = m.IsActive,
        CreatedAtUtc = m.CreatedAtUtc,
        LastLoginAtUtc = m.LastLoginAtUtc
    };

    public static TUser ToModel(this User e) => new()
    {
        Id = e.Id,
        Email = e.Email,
        PasswordHash = e.PasswordHash,
        EmailVerified = e.EmailVerified,
        IsActive = e.IsActive,
        CreatedAtUtc = e.CreatedAtUtc,
        LastLoginAtUtc = e.LastLoginAtUtc
    };

    public static RefreshToken ToEntity(this TRefreshToken m) => new()
    {
        Id = m.Id,
        UserId = m.UserId,
        TokenHash = m.TokenHash,
        CreatedAtUtc = m.CreatedAtUtc,
        ExpiresAtUtc = m.ExpiresAtUtc,
        RevokedAtUtc = m.RevokedAtUtc,
        ReplacedByTokenHash = m.ReplacedByTokenHash
    };

    public static TRefreshToken ToModel(this RefreshToken e) => new()
    {
        Id = e.Id,
        UserId = e.UserId,
        TokenHash = e.TokenHash,
        CreatedAtUtc = e.CreatedAtUtc,
        ExpiresAtUtc = e.ExpiresAtUtc,
        RevokedAtUtc = e.RevokedAtUtc,
        ReplacedByTokenHash = e.ReplacedByTokenHash
    };
}
