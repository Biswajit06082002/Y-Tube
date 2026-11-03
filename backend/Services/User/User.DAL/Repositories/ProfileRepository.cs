using Microsoft.EntityFrameworkCore;
using User.BLL.Interfaces;
using User.DAL.Context;
using User.DAL.Mappings;
using User.Entities;

namespace User.DAL.Repositories;

public sealed class ProfileRepository(UserDbContext db) : IProfileRepository
{
    public async Task<UserProfile?> GetAsync(Guid userId, CancellationToken ct = default) =>
        (await db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct))?.ToEntity();

    public async Task<IReadOnlyList<UserProfile>> GetManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) =>
        [.. (await db.Profiles.AsNoTracking().Where(p => userIds.Contains(p.UserId)).ToListAsync(ct)).Select(p => p.ToEntity())];

    public Task<bool> HandleTakenAsync(string handle, Guid exceptUserId, CancellationToken ct = default) =>
        db.Profiles.AnyAsync(p => p.Handle == handle && p.UserId != exceptUserId, ct);

    public async Task<bool> TryAddAsync(UserProfile profile, CancellationToken ct = default)
    {
        db.Profiles.Add(profile.ToModel());
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Primary key or unique handle violated: someone created it first.
            db.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task<bool> TryUpdateAsync(UserProfile profile, CancellationToken ct = default)
    {
        var row = await db.Profiles.FirstAsync(p => p.UserId == profile.UserId, ct);
        row.Handle = profile.Handle;
        row.DisplayName = profile.DisplayName;
        row.Bio = profile.Bio;
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // The unique index caught a handle that was taken between our check and the save.
            db.ChangeTracker.Clear();
            return false;
        }
    }
}
