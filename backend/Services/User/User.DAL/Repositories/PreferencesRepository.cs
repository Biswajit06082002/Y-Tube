using Microsoft.EntityFrameworkCore;
using User.BLL.Interfaces;
using User.DAL.Context;
using User.DAL.Mappings;
using User.DAL.Models;
using User.Entities;

namespace User.DAL.Repositories;

public sealed class PreferencesRepository(UserDbContext db) : IPreferencesRepository
{
    public async Task<UserPreferences?> GetAsync(Guid userId, CancellationToken ct = default) =>
        (await db.Preferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct))?.ToEntity();

    public async Task UpsertAsync(Guid userId, string theme, CancellationToken ct = default)
    {
        var row = await db.Preferences.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (row is null)
        {
            row = new TUserPreferences { UserId = userId };
            db.Preferences.Add(row);
        }

        row.Theme = theme;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
