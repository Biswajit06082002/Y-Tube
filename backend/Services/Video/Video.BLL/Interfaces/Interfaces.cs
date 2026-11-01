using Video.Entities;
using Video.Entities.Contracts;
using YTube.Common.Responses;

namespace Video.BLL.Interfaces;

// --- Implemented in the BLL (business logic) ---

public interface IVideoService
{
    Task<PagedResult<VideoDto>> ListAsync(int page, int pageSize, string? category, string? userId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> CategoriesAsync(CancellationToken ct = default);
    Task<VideoDto?> GetAsync(string id, string? userId, CancellationToken ct = default);
    Task<IReadOnlyList<VideoDto>> LikedByAsync(string userId, CancellationToken ct = default);

    /// <summary>Idempotent: liking twice is the same as liking once (safe to retry). Null when the video doesn't exist.</summary>
    Task<VideoDto?> SetLikeAsync(string id, string userId, bool liked, CancellationToken ct = default);
}

// --- Implemented in the DAL (data access) ---

public interface IVideoRepository
{
    Task<IReadOnlyList<VideoItem>> GetAllAsync(CancellationToken ct = default);
    Task<VideoItem?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default);

    Task<int> CountLikesAsync(string videoId, CancellationToken ct = default);
    Task<bool> IsLikedAsync(string videoId, string userId, CancellationToken ct = default);
    Task<IReadOnlySet<string>> GetLikedVideoIdsAsync(string userId, CancellationToken ct = default);
    Task SetLikeAsync(string videoId, string userId, bool liked, CancellationToken ct = default);
}
