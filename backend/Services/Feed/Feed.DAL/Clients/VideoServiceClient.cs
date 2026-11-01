using System.Net.Http.Json;
using Feed.BLL.Interfaces;
using Feed.Entities;
using YTube.Common.Responses;

namespace Feed.DAL.Clients;

/// <summary>
/// Feed's data source: a synchronous HTTP call to the Video service.
/// Simple, but Feed now depends on Video being up — later phases add resilience
/// (retries, circuit breaker) and an event-driven read model so Feed can answer on its own.
/// </summary>
public sealed class VideoServiceClient(HttpClient http) : IVideoCatalogClient
{
    public async Task<PagedResult<FeedVideo>> GetVideosAsync(int page, int pageSize, string? category, CancellationToken ct = default)
    {
        var url = $"api/videos?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(category))
            url += $"&category={Uri.EscapeDataString(category)}";

        var response = await http.GetFromJsonAsync<ApiResponse<PagedResult<FeedVideo>>>(url, ct);
        return response?.Data ?? PagedResult<FeedVideo>.Empty(page, pageSize);
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default)
    {
        var response = await http.GetFromJsonAsync<ApiResponse<IReadOnlyList<string>>>("api/videos/categories", ct);
        return response?.Data ?? [];
    }
}
