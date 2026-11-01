using YTube.Common.Responses;

namespace Feed.Api.Clients;

/// <summary>Shape of a video as the Video service returns it (Feed keeps its own copy of the contract).</summary>
public sealed record FeedVideo(
    string Id,
    string Title,
    string Description,
    string Category,
    string ChannelId,
    string ChannelName,
    long Views,
    int DurationSeconds,
    DateTime PublishedAtUtc,
    int LikeCount,
    bool LikedByMe);

/// <summary>
/// Synchronous service-to-service call: Feed asks Video for videos over HTTP.
/// Simple, but Feed now depends on Video being up — later phases add resilience
/// (retries, circuit breaker) and an event-driven read model so Feed can answer on its own.
/// </summary>
public sealed class VideoServiceClient(HttpClient http)
{
    public async Task<PagedResult<FeedVideo>> GetVideosAsync(int page, int pageSize, string? category, CancellationToken ct)
    {
        var url = $"api/videos?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(category))
            url += $"&category={Uri.EscapeDataString(category)}";

        var response = await http.GetFromJsonAsync<ApiResponse<PagedResult<FeedVideo>>>(url, ct);
        return response?.Data ?? PagedResult<FeedVideo>.Empty(page, pageSize);
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct)
    {
        var response = await http.GetFromJsonAsync<ApiResponse<IReadOnlyList<string>>>("api/videos/categories", ct);
        return response?.Data ?? [];
    }
}
