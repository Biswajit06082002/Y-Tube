using System.Net;
using System.Net.Http.Json;
using Comment.BLL.Interfaces;
using Comment.Entities.Contracts;
using YTube.Common.Caching;
using YTube.Common.Responses;

namespace Comment.DAL.Clients;

/// <summary>
/// Looks up a video's owner in the Video service. Every comment list needs it (creator badge,
/// who may delete), so the answer is cached in Redis for a few minutes (cache-aside): one HTTP
/// call per video every 10 minutes instead of one per page view.
/// </summary>
public sealed class VideoServiceClient(HttpClient http, ICacheService cache) : IVideoClient
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);

    public async Task<VideoInfo?> GetAsync(string videoId, CancellationToken ct = default)
    {
        var key = $"comments:video:v2:{videoId}"; // v2: shape changed (Title added) — old cached entries are ignored
        if (await cache.GetAsync<VideoInfo>(key, ct) is { } cached) return cached;

        using var response = await http.GetAsync($"api/videos/{Uri.EscapeDataString(videoId)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null; // don't cache "missing": it may be created soon
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<VideoResponse>>(ct);
        if (body?.Data is not { } data) return null;

        var info = new VideoInfo(data.Id, data.OwnerId, data.Title);
        await cache.SetAsync(key, info, CacheFor, ct);
        return info;
    }

    /// <summary>The fields we need from the Video service's VideoDto (the rest is ignored).</summary>
    private sealed record VideoResponse(string Id, Guid? OwnerId, string Title);
}
