using Microsoft.Extensions.Logging;
using User.BLL.Interfaces;
using User.Entities.Contracts;
using YTube.Common.Caching;
using YTube.Common.Results;

namespace User.BLL.Services;

/// <summary>
/// Cache-aside around the preferences repository:
///   read  → try Redis; on a miss load from the DAL and put it in Redis (with a TTL).
///   write → save through the DAL first, then DELETE the Redis key (next read reloads fresh data).
/// Preferences are read on every app start but change rarely — a perfect fit for a cache.
/// </summary>
public sealed class PreferencesService(
    IPreferencesRepository repository,
    ICacheService cache,
    ILogger<PreferencesService> logger) : IPreferencesService
{
    public static readonly string[] Themes = ["system", "daylight", "midnight", "ember", "lagoon", "sakura"];

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);
    private static string CacheKey(Guid userId) => $"user:prefs:{userId}";

    public async Task<PreferencesDto> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var cached = await TryCacheAsync(() => cache.GetAsync<PreferencesDto>(CacheKey(userId), ct));
        if (cached is not null) return cached;

        var row = await repository.GetAsync(userId, ct);
        var dto = row is null ? new PreferencesDto("system", IsDefault: true) : new PreferencesDto(row.Theme, IsDefault: false);

        await TryCacheAsync(() => cache.SetAsync(CacheKey(userId), dto, CacheTtl, ct));
        return dto;
    }

    public async Task<Result<PreferencesDto>> UpdateAsync(Guid userId, UpdatePreferencesRequest request, CancellationToken ct = default)
    {
        var theme = request.Theme?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!Themes.Contains(theme))
            return Error.Validation("invalid_theme", $"Theme must be one of: {string.Join(", ", Themes)}.");

        await repository.UpsertAsync(userId, theme, ct);

        // Invalidate rather than overwrite: if two writes race, a "set" could leave the older
        // value in the cache; a delete just forces the next read to go to the database.
        await TryCacheAsync(() => cache.RemoveAsync(CacheKey(userId), ct));

        return new PreferencesDto(theme, IsDefault: false);
    }

    /// <summary>
    /// The cache is an optimisation, not the source of truth: if Redis is down we log and carry on
    /// using SQL (graceful degradation) instead of failing the request.
    /// </summary>
    private async Task<T?> TryCacheAsync<T>(Func<Task<T?>> action)
    {
        try { return await action(); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis unavailable; falling back to the database");
            return default;
        }
    }

    private async Task TryCacheAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { logger.LogWarning(ex, "Redis unavailable; falling back to the database"); }
    }
}
