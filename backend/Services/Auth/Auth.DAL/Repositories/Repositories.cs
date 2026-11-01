using Auth.BLL.Interfaces;
using Auth.DAL.Context;
using Auth.DAL.Mappings;
using Auth.Entities;
using Microsoft.EntityFrameworkCore;

namespace Auth.DAL.Repositories;

/// <summary>
/// Repositories stage changes on the DbContext; nothing is written until the BLL calls
/// IUnitOfWork.SaveChangesAsync, so a whole business operation commits as one transaction.
/// </summary>
public sealed class UserRepository(AuthDbContext db) : IUserRepository
{
    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) =>
        db.Users.AnyAsync(u => u.Email == email, ct);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        (await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct))?.ToEntity();

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        (await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct))?.ToEntity();

    public async Task AddAsync(User user, CancellationToken ct = default) =>
        await db.Users.AddAsync(user.ToModel(), ct);

    public async Task UpdateLastLoginAsync(Guid userId, DateTime lastLoginAtUtc, CancellationToken ct = default)
    {
        var row = await db.Users.FindAsync([userId], ct);
        if (row is not null) row.LastLoginAtUtc = lastLoginAtUtc;
    }
}

public sealed class RefreshTokenRepository(AuthDbContext db) : IRefreshTokenRepository
{
    public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default) =>
        (await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct))?.ToEntity();

    public async Task AddAsync(RefreshToken token, CancellationToken ct = default) =>
        await db.RefreshTokens.AddAsync(token.ToModel(), ct);

    public async Task RevokeAsync(Guid tokenId, string? replacedByTokenHash, CancellationToken ct = default)
    {
        var row = await db.RefreshTokens.FindAsync([tokenId], ct);
        if (row is null) return;
        row.RevokedAtUtc = DateTime.UtcNow;
        row.ReplacedByTokenHash = replacedByTokenHash;
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var active = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(ct);

        foreach (var token in active)
            token.RevokedAtUtc = DateTime.UtcNow;
    }
}
