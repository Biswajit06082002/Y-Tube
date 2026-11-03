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

    /// <summary>A channel page: one creator's public videos, newest first.</summary>
    Task<PagedResult<VideoDto>> ListByChannelAsync(Guid ownerId, int page, int pageSize, string? userId, CancellationToken ct = default);
    Task<ChannelStatsDto> ChannelStatsAsync(Guid ownerId, CancellationToken ct = default);

    /// <summary>Public videos from several creators (the Following feed).</summary>
    Task<PagedResult<VideoDto>> ListByOwnersAsync(IReadOnlyCollection<Guid> ownerIds, int page, int pageSize, string? userId, CancellationToken ct = default);

    /// <summary>Idempotent: liking twice is the same as liking once (safe to retry). Null when the video doesn't exist.</summary>
    Task<VideoDto?> SetLikeAsync(string id, string userId, bool liked, CancellationToken ct = default);

    /// <summary>Stores the original, saves the DB row and queues the processing job.</summary>
    Task<Result<VideoDto>> UploadAsync(UploadVideoCommand command, CancellationToken ct = default);
}

/// <summary>
/// View counting: dedup per viewer in Redis, buffer increments in Redis, flush to SQL in batches.
/// </summary>
public interface IViewService
{
    Task<Result<ViewResultDto>> RecordViewAsync(string videoId, string viewerKey, CancellationToken ct = default);

    /// <summary>Moves buffered view counts into the database. Called every few seconds by a background job.</summary>
    Task<int> FlushPendingAsync(CancellationToken ct = default);
}

/// <summary>Keeps the denormalised channel name on videos in sync with the creator's profile.</summary>
public interface IChannelSyncHandler
{
    Task HandleAsync(UserProfileUpdated e, CancellationToken ct = default);
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
    Task<(IReadOnlyList<VideoItem> Items, int Total)> ListReadyByOwnersAsync(IReadOnlyCollection<Guid> ownerIds, int skip, int take, CancellationToken ct = default);
    Task<ChannelStatsDto> GetChannelStatsAsync(Guid ownerId, CancellationToken ct = default);
    Task<string?> GetLatestChannelNameAsync(Guid ownerId, CancellationToken ct = default);
    Task UpdateChannelNameAsync(Guid ownerId, string channelName, CancellationToken ct = default);

    /// <summary>Adds a batch of buffered views: one UPDATE ... SET Views = Views + n per video.</summary>
    Task AddViewsAsync(IReadOnlyDictionary<string, long> viewsByVideo, CancellationToken ct = default);
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

/// <summary>Fast, approximate view bookkeeping in Redis (implemented in the DAL).</summary>
public interface IViewCounterStore
{
    /// <summary>True only for the first view of this viewer within the window (atomic SET NX + TTL).</summary>
    Task<bool> TryMarkViewedAsync(string videoId, string viewerKey, TimeSpan window, CancellationToken ct = default);

    Task IncrementPendingAsync(string videoId, CancellationToken ct = default);

    /// <summary>Atomically takes all buffered counts (new views go to a fresh buffer).</summary>
    Task<IReadOnlyDictionary<string, long>> TakePendingAsync(CancellationToken ct = default);
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
