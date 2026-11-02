using Microsoft.Extensions.Logging;
using Video.BLL.Interfaces;
using Video.Entities;
using YTube.Contracts.Events;

namespace Video.BLL.Services;

/// <summary>
/// Applies the worker's events to the video's state. Handlers are idempotent and order-tolerant:
/// RabbitMQ can redeliver a message, and a late "progress" must never undo a "completed".
/// </summary>
public sealed class VideoProcessingEventsHandler(
    IVideoRepository repository,
    ILogger<VideoProcessingEventsHandler> logger) : IVideoProcessingEventsHandler
{
    public async Task HandleAsync(VideoProcessingProgress e, CancellationToken ct = default)
    {
        var video = await repository.GetByIdAsync(e.VideoId, ct);
        if (video is null || video.Status is VideoStatus.Ready or VideoStatus.Failed) return;

        await repository.UpdateStatusAsync(e.VideoId, VideoStatus.Processing, Math.Clamp(e.Percent, 0, 99), null, ct);
    }

    public async Task HandleAsync(VideoProcessingCompleted e, CancellationToken ct = default)
    {
        if (await repository.GetByIdAsync(e.VideoId, ct) is null) return;

        await repository.CompleteProcessingAsync(
            e.VideoId, e.DurationSeconds, e.Width, e.Height,
            [.. e.Renditions.Select(r => r.Name)],
            e.MasterPlaylistPath, e.ThumbnailPath, ct);

        logger.LogInformation("Video {VideoId} is ready ({Renditions})", e.VideoId, string.Join(", ", e.Renditions.Select(r => r.Name)));
    }

    public async Task HandleAsync(VideoProcessingFailed e, CancellationToken ct = default)
    {
        var video = await repository.GetByIdAsync(e.VideoId, ct);
        if (video is null || video.Status == VideoStatus.Ready) return;

        await repository.UpdateStatusAsync(e.VideoId, VideoStatus.Failed, video.ProcessingProgress, e.Reason, ct);
        logger.LogWarning("Video {VideoId} failed processing: {Reason}", e.VideoId, e.Reason);
    }
}
