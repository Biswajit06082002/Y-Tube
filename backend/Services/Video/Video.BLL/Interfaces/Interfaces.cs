using Video.Entities;
using Video.Entities.Contracts;
using YTube.Common.Responses;
using YTube.Common.Results;
using YTube.Contracts.Events;

namespace Video.BLL.Interfaces;

// --- Implemented in the BLL (business logic) ---

public interface IVideoService
{
    /// <summary>Public list: only videos whose processing finished (status Ready).</summary>
    Task<PagedResult<VideoDto>> ListAsync(int page, int pageSize, string? category, string? userId, CancellationToken ct = default);
    IReadOnlyList<string> Categories();

    /// <summary>Ready videos are public; a video still processing is only visible to its owner.</summary>
    Task<VideoDto?> GetAsync(string id, string? userId, CancellationToken ct = default);

    Task<IReadOnlyList<VideoDto>> LikedByAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<VideoDto>> ListMineAsync(Guid ownerId, CancellationToken ct = default);

    /// <summary>Idempotent: liking twice is the same as liking once (safe to retry). Null when the video doesn't exist.</summary>
    Task<VideoDto?> SetLikeAsync(string id, string userId, bool liked, CancellationToken ct = default);

    /// <summary>Stores the original, saves the DB row and queues the processing job.</summary>
    Task<Result<VideoDto>> UploadAsync(UploadVideoCommand command, CancellationToken ct = default);
}

/// <summary>Reacts to events published by the Processing worker.</summary>
public interface IVideoProcessingEventsHandler
{
    Task HandleAsync(VideoProcessingProgress e, CancellationToken ct = default);
    Task HandleAsync(VideoProcessingCompleted e, CancellationToken ct = default);
    Task HandleAsync(VideoProcessingFailed e, CancellationToken ct = default);
}

// --- Implemented in the DAL (database + file storage) ---

public interface IVideoRepository
{
    Task<(IReadOnlyList<VideoItem> Items, int Total)> ListReadyAsync(int skip, int take, string? category, CancellationToken ct = default);
    Task<IReadOnlyList<VideoItem>> ListByOwnerAsync(Guid ownerId, CancellationToken ct = default);
    Task<IReadOnlyList<VideoItem>> GetByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct = default);
    Task<VideoItem?> GetByIdAsync(string id, CancellationToken ct = default);

    Task AddAsync(VideoItem video, CancellationToken ct = default);
    Task UpdateStatusAsync(string id, VideoStatus status, int progress, string? error, CancellationToken ct = default);
    Task CompleteProcessingAsync(string id, int durationSeconds, int width, int height, IReadOnlyList<string> renditions,
        string masterPlaylistPath, string? thumbnailPath, CancellationToken ct = default);

    // Likes are read in batches for a whole page of videos — one query instead of one per video (avoids N+1).
    Task<IReadOnlyDictionary<string, int>> CountLikesAsync(IReadOnlyCollection<string> videoIds, CancellationToken ct = default);
    Task<IReadOnlySet<string>> GetLikedAmongAsync(string userId, IReadOnlyCollection<string> videoIds, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetLikedVideoIdsAsync(string userId, CancellationToken ct = default);
    Task SetLikeAsync(string videoId, string userId, bool liked, CancellationToken ct = default);
}

/// <summary>Where media files live. Local disk today; object storage (S3 / Azure Blob) in production.</summary>
public interface IMediaStorage
{
    /// <summary>Stores the original upload. Returns its path relative to the media root.</summary>
    Task<string> SaveSourceAsync(string videoId, Stream content, string extension, CancellationToken ct = default);

    /// <summary>Stores a user-chosen thumbnail. Returns its path relative to the media root.</summary>
    Task<string> SaveThumbnailAsync(string videoId, Stream content, string extension, CancellationToken ct = default);

    /// <summary>Removes every file of a video (used when an upload can't be queued).</summary>
    Task DeleteAllAsync(string videoId, CancellationToken ct = default);
}
