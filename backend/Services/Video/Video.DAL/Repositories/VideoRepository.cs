using Microsoft.EntityFrameworkCore;
using Video.BLL.Interfaces;
using Video.DAL.Context;
using Video.DAL.Mappings;
using Video.DAL.Models;
using Video.Entities;

namespace Video.DAL.Repositories;

public sealed class VideoRepository(VideoDbContext db) : IVideoRepository
{
    private const int ReadyStatus = (int)VideoStatus.Ready;

    public async Task<(IReadOnlyList<VideoItem> Items, int Total)> ListReadyAsync(int skip, int take, string? category, CancellationToken ct = default)
    {
        var query = db.Videos.AsNoTracking().Where(v => v.Status == ReadyStatus);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(v => v.Category == category);

        // Filtering and paging run in SQL (OFFSET/FETCH), not in memory.
        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(v => v.PublishedAtUtc).ThenBy(v => v.Id)
            .Skip(skip).Take(take)
            .Include(v => v.Tags)
            .AsSplitQuery()
            .ToListAsync(ct);

        return ([.. rows.Select(r => r.ToEntity())], total);
    }

    public async Task<IReadOnlyList<VideoItem>> ListByOwnerAsync(Guid ownerId, CancellationToken ct = default) =>
        [.. (await db.Videos.AsNoTracking()
                .Where(v => v.OwnerId == ownerId)
                .OrderByDescending(v => v.CreatedAtUtc)
                .Include(v => v.Tags)
                .AsSplitQuery()
                .ToListAsync(ct))
            .Select(r => r.ToEntity())];

    public async Task<IReadOnlyList<VideoItem>> GetByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct = default) =>
        [.. (await db.Videos.AsNoTracking()
                .Where(v => ids.Contains(v.Id))
                .Include(v => v.Tags)
                .AsSplitQuery()
                .ToListAsync(ct))
            .Select(r => r.ToEntity())];

    public async Task<VideoItem?> GetByIdAsync(string id, CancellationToken ct = default) =>
        (await db.Videos.AsNoTracking().Include(v => v.Tags).FirstOrDefaultAsync(v => v.Id == id, ct))?.ToEntity();

    public async Task AddAsync(VideoItem video, CancellationToken ct = default)
    {
        db.Videos.Add(video.ToModel());
        await db.SaveChangesAsync(ct);
    }

    public Task UpdateStatusAsync(string id, VideoStatus status, int progress, string? error, CancellationToken ct = default) =>
        // ExecuteUpdate: a single UPDATE statement, no need to load the row first.
        db.Videos.Where(v => v.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(v => v.Status, (int)status)
            .SetProperty(v => v.ProcessingProgress, progress)
            .SetProperty(v => v.ProcessingError, error), ct);

    public async Task CompleteProcessingAsync(string id, int durationSeconds, int width, int height, IReadOnlyList<string> renditions,
        string masterPlaylistPath, string? thumbnailPath, CancellationToken ct = default)
    {
        var row = await db.Videos.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (row is null) return;

        row.Status = ReadyStatus;
        row.ProcessingProgress = 100;
        row.ProcessingError = null;
        row.DurationSeconds = durationSeconds;
        row.Width = width;
        row.Height = height;
        row.Renditions = string.Join(',', renditions);
        row.MasterPlaylistPath = masterPlaylistPath;
        row.ThumbnailPath = thumbnailPath ?? row.ThumbnailPath; // keep a custom thumbnail if the worker didn't make one
        row.PublishedAtUtc ??= DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyDictionary<string, int>> CountLikesAsync(IReadOnlyCollection<string> videoIds, CancellationToken ct = default) =>
        await db.VideoLikes.AsNoTracking()
            .Where(l => videoIds.Contains(l.VideoId))
            .GroupBy(l => l.VideoId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    public async Task<IReadOnlySet<string>> GetLikedAmongAsync(string userId, IReadOnlyCollection<string> videoIds, CancellationToken ct = default) =>
        (await db.VideoLikes.AsNoTracking()
            .Where(l => l.UserId == userId && videoIds.Contains(l.VideoId))
            .Select(l => l.VideoId)
            .ToListAsync(ct)).ToHashSet();

    public async Task<IReadOnlyList<string>> GetLikedVideoIdsAsync(string userId, CancellationToken ct = default) =>
        await db.VideoLikes.AsNoTracking()
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .Select(l => l.VideoId)
            .ToListAsync(ct);

    public async Task SetLikeAsync(string videoId, string userId, bool liked, CancellationToken ct = default)
    {
        var exists = await db.VideoLikes.AnyAsync(l => l.VideoId == videoId && l.UserId == userId, ct);
        if (liked && !exists)
        {
            db.VideoLikes.Add(new TVideoLike { VideoId = videoId, UserId = userId, CreatedAtUtc = DateTime.UtcNow });
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Two identical likes raced; the primary key kept just one. That's the desired end state.
            }
        }
        else if (!liked && exists)
        {
            await db.VideoLikes.Where(l => l.VideoId == videoId && l.UserId == userId).ExecuteDeleteAsync(ct);
        }
    }
}
