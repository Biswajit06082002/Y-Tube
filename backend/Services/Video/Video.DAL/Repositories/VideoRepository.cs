using System.Collections.Concurrent;
using Video.BLL.Interfaces;
using Video.DAL.Context;
using Video.DAL.Mappings;
using Video.Entities;

namespace Video.DAL.Repositories;

/// <summary>
/// Reads/writes through the in-memory context today. Swapping in SQL later means a new context
/// and query code here — the BLL depends on IVideoRepository, so it doesn't change.
/// </summary>
public sealed class VideoRepository(InMemoryVideoContext db) : IVideoRepository
{
    public Task<IReadOnlyList<VideoItem>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<VideoItem>>([.. db.Videos.Select(v => v.ToEntity())]);

    public Task<VideoItem?> GetByIdAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(db.VideosById.TryGetValue(id, out var row) ? row.ToEntity() : null);

    public Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default) =>
        Task.FromResult(db.Categories);

    public Task<int> CountLikesAsync(string videoId, CancellationToken ct = default) =>
        Task.FromResult(db.Likes.TryGetValue(videoId, out var users) ? users.Count : 0);

    public Task<bool> IsLikedAsync(string videoId, string userId, CancellationToken ct = default) =>
        Task.FromResult(db.Likes.TryGetValue(videoId, out var users) && users.ContainsKey(userId));

    public Task<IReadOnlySet<string>> GetLikedVideoIdsAsync(string userId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlySet<string>>(db.Likes.Where(kv => kv.Value.ContainsKey(userId)).Select(kv => kv.Key).ToHashSet());

    public Task SetLikeAsync(string videoId, string userId, bool liked, CancellationToken ct = default)
    {
        var users = db.Likes.GetOrAdd(videoId, _ => new ConcurrentDictionary<string, byte>());
        if (liked) users.TryAdd(userId, 0);
        else users.TryRemove(userId, out _);
        return Task.CompletedTask;
    }
}
