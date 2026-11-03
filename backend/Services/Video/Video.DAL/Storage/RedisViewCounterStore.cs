using StackExchange.Redis;
using Video.BLL.Interfaces;

namespace Video.DAL.Storage;

/// <summary>
/// Redis keys:
///   video:viewed:{videoId}:{viewer}  — dedup marker, expires after the window (SET NX EX)
///   video:views:pending              — hash videoId → views not yet in SQL (HINCRBY)
/// </summary>
public sealed class RedisViewCounterStore(IConnectionMultiplexer redis) : IViewCounterStore
{
    private const string PendingKey = "video:views:pending";
    private readonly IDatabase _db = redis.GetDatabase();

    public Task<bool> TryMarkViewedAsync(string videoId, string viewerKey, TimeSpan window, CancellationToken ct = default) =>
        // Only succeeds if the key doesn't exist yet — atomic, so two parallel requests can't both count.
        _db.StringSetAsync($"video:viewed:{videoId}:{viewerKey}", 1, window, When.NotExists);

    public Task IncrementPendingAsync(string videoId, CancellationToken ct = default) =>
        _db.HashIncrementAsync(PendingKey, videoId, 1);

    public async Task<IReadOnlyDictionary<string, long>> TakePendingAsync(CancellationToken ct = default)
    {
        if (!await _db.KeyExistsAsync(PendingKey)) return new Dictionary<string, long>();

        // RENAME is atomic: views arriving from now on start a fresh "pending" hash, and we own this batch.
        var batchKey = $"video:views:flushing:{Guid.NewGuid():N}";
        try
        {
            await _db.KeyRenameAsync(PendingKey, batchKey);
        }
        catch (RedisServerException)
        {
            return new Dictionary<string, long>(); // another flusher took it first
        }

        var entries = await _db.HashGetAllAsync(batchKey);
        await _db.KeyDeleteAsync(batchKey);
        return entries.ToDictionary(e => e.Name.ToString(), e => (long)e.Value);
    }
}
