using Microsoft.EntityFrameworkCore;
using User.BLL.Interfaces;
using User.DAL.Data;
using User.Entities;

namespace User.DAL.Repositories;

public sealed class PreferencesRepository(UserDbContext db) : IPreferencesRepository
{
    public Task<UserPreferences?> GetAsync(Guid userId, CancellationToken ct = default) =>
        db.Preferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct);

    public async Task UpsertAsync(Guid userId, string theme, CancellationToken ct = default)
    {
        var row = await db.Preferences.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (row is null)
        {
            row = new UserPreferences { UserId = userId };
            db.Preferences.Add(row);
        }

        row.Theme = theme;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
