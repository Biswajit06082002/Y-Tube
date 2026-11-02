using System.Globalization;
using Microsoft.Extensions.Options;
using Processing.BLL.Interfaces;
using Processing.Entities;

namespace Processing.DAL.Ffmpeg;

/// <summary>Encodes one HLS rendition with FFmpeg (H.264 video + AAC audio, MPEG-TS segments).</summary>
public sealed class FfmpegTranscoder(IOptions<FfmpegOptions> options) : ITranscoder
{
    private readonly FfmpegOptions _options = options.Value;

    public async Task TranscodeToHlsAsync(string inputPath, string outputDirectory, RenditionPlan plan, double durationSeconds,
        Func<double, Task> onProgress, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var p = plan.Profile;
        var seg = _options.SegmentSeconds;
        string Kbps(double value) => ((int)value).ToString(CultureInfo.InvariantCulture) + "k";

        string[] args =
        [
            "-hide_banner", "-y",
            "-i", inputPath,
            "-map", "0:v:0", "-map", "0:a:0?",             // first video stream, first audio stream if there is one
            "-vf", $"scale={plan.Width}:{plan.Height}",
            "-c:v", "libx264", "-preset", _options.Preset, "-profile:v", "main", "-pix_fmt", "yuv420p",
            // Capped bitrate (target / max / buffer) so each rendition really fits the bandwidth it advertises.
            "-b:v", Kbps(p.VideoKbps), "-maxrate", Kbps(p.VideoKbps * 1.07), "-bufsize", Kbps(p.VideoKbps * 1.5),
            // A keyframe exactly every segment, at the same timestamps in every rendition: this is what
            // lets the player switch quality at any segment boundary without glitches.
            "-force_key_frames", $"expr:gte(t,n_forced*{seg})", "-sc_threshold", "0",
            "-c:a", "aac", "-b:a", Kbps(p.AudioKbps), "-ac", "2", "-ar", "48000",
            "-f", "hls", "-hls_time", seg.ToString(CultureInfo.InvariantCulture),
            "-hls_playlist_type", "vod", "-hls_flags", "independent_segments",
            "-hls_segment_filename", Path.Combine(outputDirectory, "seg_%04d.ts"),
            "-progress", "pipe:1", "-nostats",                 // machine-readable progress on stdout
            Path.Combine(outputDirectory, "index.m3u8")
        ];

        var result = await ProcessRunner.RunAsync(_options.FfmpegPath, args, async line =>
        {
            // -progress prints "out_time_us=12345678" (microseconds encoded so far).
            if (!line.StartsWith("out_time_us=", StringComparison.Ordinal) || durationSeconds <= 0) return;
            if (long.TryParse(line.AsSpan("out_time_us=".Length), out var micros) && micros > 0)
                await onProgress(Math.Clamp(micros / 1_000_000d / durationSeconds, 0, 1));
        }, ct);

        if (result.ExitCode != 0)
            throw new InvalidOperationException($"ffmpeg failed for {p.Name}: {result.StdErrTail}");

        await onProgress(1);
    }
}
