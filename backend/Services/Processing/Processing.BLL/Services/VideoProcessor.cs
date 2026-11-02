using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Processing.BLL.Interfaces;
using Processing.Entities;
using YTube.Common.Messaging;
using YTube.Contracts.Events;

namespace Processing.BLL.Services;

/// <summary>
/// The processing pipeline for one upload:
///   1. probe     — read duration, resolution, audio
///   2. ladder    — pick the renditions this source supports (e.g. 720p source → 720/480/360/240/144)
///   3. encode    — one HLS rendition at a time, with aligned keyframes so players can switch mid-play
///   4. master    — master.m3u8 lists every rendition; the player picks/switches between them
///   5. thumbnail — a frame at ~10% of the video, unless the uploader chose one
///   6. publish   — VideoProcessingCompleted (or VideoProcessingFailed)
/// </summary>
public sealed class VideoProcessor(
    IMediaProbe probe,
    ITranscoder transcoder,
    IThumbnailExtractor thumbnails,
    IMediaFiles files,
    IMessagePublisher publisher,
    ILogger<VideoProcessor> logger) : IVideoProcessor
{
    public async Task ProcessAsync(VideoUploaded job, CancellationToken ct = default)
    {
        var progress = new ProgressReporter(publisher, job.VideoId);
        try
        {
            await progress.ReportAsync(1, "Analyzing", force: true, ct);

            var input = files.AbsolutePath(job.SourcePath);
            if (!File.Exists(input))
                throw new ProcessingException("The original file is missing.");

            MediaInfo info;
            try
            {
                info = await probe.ProbeAsync(input, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Could not read {VideoId}", job.VideoId);
                throw new ProcessingException("This file isn't a video we can read. It may be corrupted or in an unsupported format.");
            }

            if (info.Width <= 0 || info.Height <= 0)
                throw new ProcessingException("No video stream was found in this file.");
            if (info.DurationSeconds <= 0)
                throw new ProcessingException("Couldn't read the video's duration.");

            var ladder = RenditionLadder.For(info.Width, info.Height);
            var hls = files.PrepareHlsDirectory(job.VideoId);
            logger.LogInformation("Processing {VideoId}: {W}x{H}, {Duration:F1}s → {Renditions}",
                job.VideoId, info.Width, info.Height, info.DurationSeconds, string.Join(", ", ladder.Select(r => r.Profile.Name)));

            // Encoding is ~90% of the work; split it evenly across the renditions.
            for (var i = 0; i < ladder.Count; i++)
            {
                var plan = ladder[i];
                var index = i;
                await transcoder.TranscodeToHlsAsync(
                    input, Path.Combine(hls.AbsoluteDirectory, plan.Profile.Name), plan, info.DurationSeconds,
                    fraction => progress.ReportAsync(5 + (int)(90 * (index + fraction) / ladder.Count), $"Encoding {plan.Profile.Name}", false, ct),
                    ct);
            }

            var masterRelative = $"{hls.RelativeDirectory}/master.m3u8";
            await files.WriteTextAsync(files.AbsolutePath(masterRelative), BuildMasterPlaylist(ladder), ct);

            string? thumbnailRelative = null;
            if (!job.HasCustomThumbnail)
            {
                await progress.ReportAsync(96, "Creating thumbnail", force: true, ct);
                thumbnailRelative = $"videos/{job.VideoId}/thumbnail.jpg";
                var at = Math.Clamp(info.DurationSeconds * 0.1, 0, Math.Max(0, info.DurationSeconds - 0.5));
                await thumbnails.ExtractAsync(input, files.AbsolutePath(thumbnailRelative), at, ct);
            }

            await publisher.PublishAsync(RoutingKeys.ProcessingCompleted, new VideoProcessingCompleted(
                job.VideoId,
                (int)Math.Round(info.DurationSeconds),
                info.Width,
                info.Height,
                [.. ladder.Select(r => new RenditionInfo(r.Profile.Name, r.Width, r.Height, r.Profile.VideoKbps + r.Profile.AudioKbps))],
                masterRelative,
                thumbnailRelative), ct);

            logger.LogInformation("Video {VideoId} processed", job.VideoId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // shutting down: let the message go back to the queue for another worker
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Processing failed for {VideoId}", job.VideoId);
            files.DeleteHlsDirectory(job.VideoId);
            var reason = ex is ProcessingException ? ex.Message : "Something went wrong while processing this video.";
            await publisher.PublishAsync(RoutingKeys.ProcessingFailed, new VideoProcessingFailed(job.VideoId, reason), ct);
        }
    }

    /// <summary>
    /// master.m3u8 — the entry point the player loads. Each entry advertises a rendition's peak
    /// bandwidth and resolution; the player measures its download speed and picks the best fit,
    /// and can jump to another rendition at any segment boundary.
    /// </summary>
    private static string BuildMasterPlaylist(IReadOnlyList<RenditionPlan> ladder)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#EXTM3U");
        sb.AppendLine("#EXT-X-VERSION:3");
        sb.AppendLine("#EXT-X-INDEPENDENT-SEGMENTS");
        foreach (var r in ladder)
        {
            var average = (r.Profile.VideoKbps + r.Profile.AudioKbps) * 1000;
            var peak = (int)(r.Profile.VideoKbps * 1.07 + r.Profile.AudioKbps) * 1000;
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"#EXT-X-STREAM-INF:BANDWIDTH={peak},AVERAGE-BANDWIDTH={average},RESOLUTION={r.Width}x{r.Height},NAME=\"{r.Profile.Name}\""));
            sb.AppendLine($"{r.Profile.Name}/index.m3u8");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Progress updates are throttled (every 2% / 750 ms at most): FFmpeg reports many times a
    /// second, and flooding the broker and the Video database with updates helps nobody.
    /// </summary>
    private sealed class ProgressReporter(IMessagePublisher publisher, string videoId)
    {
        private int _lastPercent = -1;
        private string _lastStage = string.Empty;
        private DateTime _lastSentUtc = DateTime.MinValue;

        public async Task ReportAsync(int percent, string stage, bool force, CancellationToken ct)
        {
            percent = Math.Clamp(percent, 0, 99);
            var due = force || stage != _lastStage ||
                      (percent >= _lastPercent + 2 && DateTime.UtcNow - _lastSentUtc > TimeSpan.FromMilliseconds(750));
            if (!due) return;

            _lastPercent = percent;
            _lastStage = stage;
            _lastSentUtc = DateTime.UtcNow;
            await publisher.PublishAsync(RoutingKeys.ProcessingProgress, new VideoProcessingProgress(videoId, percent, stage), ct);
        }
    }
}

/// <summary>A processing failure whose message is safe to show to the uploader.</summary>
public sealed class ProcessingException(string message) : Exception(message);
