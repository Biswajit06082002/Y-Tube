using Video.BLL.Interfaces;
using Video.Entities;
using Video.Entities.Contracts;
using YTube.Common.Responses;

namespace Video.BLL.Services;

/// <summary>
/// Video business rules: paging limits, category filtering, and turning stored videos into
/// what a particular caller sees (like count + whether *they* liked it).
/// </summary>
public sealed class VideoService(IVideoRepository repository) : IVideoService
{
    private const int MaxPageSize = 50;

    public async Task<PagedResult<VideoDto>> ListAsync(int page, int pageSize, string? category, string? userId, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var all = await repository.GetAllAsync(ct);
        var filtered = string.IsNullOrWhiteSpace(category)
            ? all
            : all.Where(v => v.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();

        var items = new List<VideoDto>();
        foreach (var video in filtered.Skip((page - 1) * pageSize).Take(pageSize))
            items.Add(await ToDtoAsync(video, userId, ct));

        return new PagedResult<VideoDto>(items, page, pageSize, filtered.Count);
    }

    public Task<IReadOnlyList<string>> CategoriesAsync(CancellationToken ct = default) => repository.GetCategoriesAsync(ct);

    public async Task<VideoDto?> GetAsync(string id, string? userId, CancellationToken ct = default) =>
        await repository.GetByIdAsync(id, ct) is { } video ? await ToDtoAsync(video, userId, ct) : null;

    public async Task<IReadOnlyList<VideoDto>> LikedByAsync(string userId, CancellationToken ct = default)
    {
        var likedIds = await repository.GetLikedVideoIdsAsync(userId, ct);
        var result = new List<VideoDto>();
        foreach (var video in (await repository.GetAllAsync(ct)).Where(v => likedIds.Contains(v.Id)))
            result.Add(await ToDtoAsync(video, userId, ct));
        return result;
    }

    public async Task<VideoDto?> SetLikeAsync(string id, string userId, bool liked, CancellationToken ct = default)
    {
        var video = await repository.GetByIdAsync(id, ct);
        if (video is null) return null;

        await repository.SetLikeAsync(id, userId, liked, ct);
        return await ToDtoAsync(video, userId, ct);
    }

    private async Task<VideoDto> ToDtoAsync(VideoItem v, string? userId, CancellationToken ct)
    {
        var likes = v.BaseLikes + await repository.CountLikesAsync(v.Id, ct);
        var likedByMe = userId is not null && await repository.IsLikedAsync(v.Id, userId, ct);
        return new VideoDto(v.Id, v.Title, v.Description, v.Category, v.ChannelId, v.ChannelName,
            v.Views, v.DurationSeconds, v.PublishedAtUtc, likes, likedByMe);
    }
}
