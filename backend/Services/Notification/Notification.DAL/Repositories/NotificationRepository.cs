using Microsoft.EntityFrameworkCore;
using Notification.BLL.Interfaces;
using Notification.DAL.Context;
using Notification.DAL.Mappings;
using Notification.Entities;

namespace Notification.DAL.Repositories;

public sealed class NotificationRepository(NotificationDbContext db) : INotificationRepository
{
    public async Task<IReadOnlyList<NotificationItem>> AddNewAsync(IReadOnlyList<NotificationItem> items, CancellationToken ct = default)
    {
        // Skip what already exists (a redelivered event): one query per DedupKey in the batch.
        var fresh = new List<NotificationItem>();
        foreach (var group in items.GroupBy(i => i.DedupKey))
        {
            var userIds = group.Select(i => i.UserId).Distinct().ToList();
            var existing = (await db.Notifications.AsNoTracking()
                .Where(n => n.DedupKey == group.Key && userIds.Contains(n.UserId))
                .Select(n => n.UserId)
                .ToListAsync(ct)).ToHashSet();
            fresh.AddRange(group.Where(i => existing.Add(i.UserId))); // Add() also drops duplicates inside the batch
        }
        if (fresh.Count == 0) return [];

        var rows = fresh.Select(i => i.ToModel()).ToList();
        db.Notifications.AddRange(rows);
        try
        {
            await db.SaveChangesAsync(ct);
            return [.. rows.Select(r => r.ToEntity())];
        }
        catch (DbUpdateException)
        {
            // Another consumer inserted some of them between our check and the save (the unique index
            // caught it). Fall back to one row at a time, keeping whichever ones are still new.
            db.ChangeTracker.Clear();
            var saved = new List<NotificationItem>();
            foreach (var row in fresh.Select(i => i.ToModel()))
            {
                db.Notifications.Add(row);
                try
                {
                    await db.SaveChangesAsync(ct);
                    saved.Add(row.ToEntity());
                }
                catch (DbUpdateException)
                {
                    db.ChangeTracker.Clear();
                }
            }
            return saved;
        }
    }

    public async Task<IReadOnlyList<NotificationItem>> GetPageAsync(Guid userId, long? beforeId, int take, CancellationToken ct = default)
    {
        var query = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (beforeId is { } before) query = query.Where(n => n.Id < before);

        var rows = await query.OrderByDescending(n => n.Id).Take(take).ToListAsync(ct);
        return [.. rows.Select(r => r.ToEntity())];
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default) =>
        db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    // "AND UserId = @me" makes sure nobody can mark someone else's notification by guessing ids.
    public Task MarkReadAsync(Guid userId, long id, CancellationToken ct = default) =>
        db.Notifications.Where(n => n.Id == id && n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);

    public Task MarkAllReadAsync(Guid userId, CancellationToken ct = default) =>
        db.Notifications.Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
}
