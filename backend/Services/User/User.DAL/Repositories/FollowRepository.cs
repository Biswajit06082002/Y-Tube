using Microsoft.EntityFrameworkCore;
using User.BLL.Interfaces;
using User.DAL.Context;
using User.DAL.Models;

namespace User.DAL.Repositories;

public sealed class FollowRepository(UserDbContext db) : IFollowRepository
{
    public async Task<bool> SetFollowAsync(Guid followerId, Guid followeeId, bool follow, CancellationToken ct = default)
    {
        if (follow)
        {
            if (await IsFollowingAsync(followerId, followeeId, ct)) return false;
            db.Follows.Add(new TFollow { FollowerId = followerId, FolloweeId = followeeId, CreatedAtUtc = DateTime.UtcNow });
            try
            {
                await db.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear(); // a parallel request inserted it first — same end state
                return false;
            }
        }

        await db.Follows.Where(f => f.FollowerId == followerId && f.FolloweeId == followeeId).ExecuteDeleteAsync(ct);
        return false;
    }

    public Task<bool> IsFollowingAsync(Guid followerId, Guid followeeId, CancellationToken ct = default) =>
        db.Follows.AnyAsync(f => f.FollowerId == followerId && f.FolloweeId == followeeId, ct);

    public Task<int> CountFollowersAsync(Guid userId, CancellationToken ct = default) =>
        db.Follows.CountAsync(f => f.FolloweeId == userId, ct);

    public Task<int> CountFollowingAsync(Guid userId, CancellationToken ct = default) =>
        db.Follows.CountAsync(f => f.FollowerId == userId, ct);

    public async Task<IReadOnlyList<Guid>> GetFolloweeIdsAsync(Guid followerId, CancellationToken ct = default) =>
        await db.Follows.AsNoTracking()
            .Where(f => f.FollowerId == followerId)
            .OrderByDescending(f => f.CreatedAtUtc)
            .Select(f => f.FolloweeId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetFollowerIdsAsync(Guid followeeId, CancellationToken ct = default) =>
        await db.Follows.AsNoTracking()
            .Where(f => f.FolloweeId == followeeId) // served by the FolloweeId index
            .Select(f => f.FollowerId)
            .ToListAsync(ct);
}
