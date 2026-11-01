namespace YTube.Common.Caching;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>Remaining time-to-live of a key, or null when it doesn't exist / never expires.</summary>
    Task<TimeSpan?> GetTimeToLiveAsync(string key, CancellationToken ct = default);
}
