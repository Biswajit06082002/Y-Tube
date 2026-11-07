using Microsoft.Extensions.Logging;
using Video.BLL.Interfaces;
using Video.Entities;
using YTube.Common.Messaging;
using YTube.Contracts.Events;

namespace Video.BLL.Services;

/// <summary>
/// Applies the worker's events to the video's state. Handlers are idempotent and order-tolerant:
/// RabbitMQ can redeliver a message, and a late "progress" must never undo a "completed".
/// </summary>
public sealed class VideoProcessingEventsHandler(
    IVideoRepository repository,
    IMessagePublisher publisher,
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
        var video = await repository.GetByIdAsync(e.VideoId, ct);
        if (video is null) return;
        var wasReady = video.Status == VideoStatus.Ready; // a redelivered "completed" must not notify twice

        await repository.CompleteProcessingAsync(
            e.VideoId, e.DurationSeconds, e.Width, e.Height,
            [.. e.Renditions.Select(r => r.Name)],
            e.MasterPlaylistPath, e.ThumbnailPath, ct);

        logger.LogInformation("Video {VideoId} is ready ({Renditions})", e.VideoId, string.Join(", ", e.Renditions.Select(r => r.Name)));

        if (!wasReady && video.OwnerId is { } ownerId)
        {
            // The video is now watchable: tell the rest of the system (followers get notified).
            // If this publish throws, the message is nacked to the DLQ — see "dual writes" in SYSTEM-DESIGN step 7.
            await publisher.PublishAsync(RoutingKeys.VideoPublished, new VideoPublished(
                video.Id, ownerId, video.ChannelName, video.Title,
                e.ThumbnailPath is null ? null : "/media/" + e.ThumbnailPath,
                DateTime.UtcNow), ct);
        }
    }

    public async Task HandleAsync(VideoProcessingFailed e, CancellationToken ct = default)
    {
        var video = await repository.GetByIdAsync(e.VideoId, ct);
        if (video is null || video.Status == VideoStatus.Ready) return;

        await repository.UpdateStatusAsync(e.VideoId, VideoStatus.Failed, video.ProcessingProgress, e.Reason, ct);
        logger.LogWarning("Video {VideoId} failed processing: {Reason}", e.VideoId, e.Reason);
    }
}
