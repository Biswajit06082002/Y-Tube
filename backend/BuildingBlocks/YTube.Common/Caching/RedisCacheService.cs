using System.Text.Json;
using StackExchange.Redis;

namespace YTube.Common.Caching;

public sealed class RedisCacheService(IConnectionMultiplexer redis) : ICacheService
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var value = await _db.StringGetAsync(key);
        return value.HasValue ? JsonSerializer.Deserialize<T>(value.ToString()) : default;
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(value);
        return expiry is { } ttl
            ? _db.StringSetAsync(key, json, ttl)
            : _db.StringSetAsync(key, json);
    }

    public Task RemoveAsync(string key, CancellationToken ct = default) => _db.KeyDeleteAsync(key);

    public Task<TimeSpan?> GetTimeToLiveAsync(string key, CancellationToken ct = default) => _db.KeyTimeToLiveAsync(key);
}
