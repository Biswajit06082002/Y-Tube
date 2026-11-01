using Microsoft.EntityFrameworkCore;
using User.Api.Data;
using YTube.Common.Caching;
using YTube.Common.Results;

namespace User.Api.Preferences;

/// <param name="IsDefault">True when the user never saved anything (the frontend then keeps the guest's local choice).</param>
public sealed record PreferencesDto(string Theme, bool IsDefault);

public sealed record UpdatePreferencesRequest(string Theme);

/// <summary>
/// Cache-aside around the preferences table:
///   read  → try Redis; on a miss load from SQL and put it in Redis (with a TTL).
///   write → update SQL first, then DELETE the Redis key (next read reloads fresh data).
/// Preferences are read on every app start but change rarely — a perfect fit for a cache.
/// </summary>
public sealed class PreferencesService(UserDbContext db, ICacheService cache, ILogger<PreferencesService> logger)
{
    public static readonly string[] Themes = ["system", "daylight", "midnight", "ember", "lagoon", "sakura"];

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);
    private static string CacheKey(Guid userId) => $"user:prefs:{userId}";

    public async Task<PreferencesDto> GetAsync(Guid userId, CancellationToken ct)
    {
        var cached = await TryCacheAsync(() => cache.GetAsync<PreferencesDto>(CacheKey(userId), ct));
        if (cached is not null) return cached;

        var row = await db.Preferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct);
        var dto = row is null ? new PreferencesDto("system", IsDefault: true) : new PreferencesDto(row.Theme, IsDefault: false);

        await TryCacheAsync(() => cache.SetAsync(CacheKey(userId), dto, CacheTtl, ct));
        return dto;
    }

    public async Task<Result<PreferencesDto>> UpdateAsync(Guid userId, UpdatePreferencesRequest request, CancellationToken ct)
    {
        var theme = request.Theme?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!Themes.Contains(theme))
            return Error.Validation("invalid_theme", $"Theme must be one of: {string.Join(", ", Themes)}.");

        // Upsert: first save creates the row, later saves update it.
        var row = await db.Preferences.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (row is null)
        {
            row = new UserPreferences { UserId = userId };
            db.Preferences.Add(row);
        }
        row.Theme = theme;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

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
