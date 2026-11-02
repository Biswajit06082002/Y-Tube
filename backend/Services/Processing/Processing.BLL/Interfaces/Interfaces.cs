using Processing.Entities;
using YTube.Contracts.Events;

namespace Processing.BLL.Interfaces;

// --- Implemented in the BLL ---

public interface IVideoProcessor
{
    /// <summary>
    /// Turns one uploaded original into HLS renditions + a thumbnail and publishes the outcome.
    /// Business failures (corrupt file, no video stream) are published as VideoProcessingFailed,
    /// not thrown — only shutdown/cancellation escapes.
    /// </summary>
    Task ProcessAsync(VideoUploaded job, CancellationToken ct = default);
}

// --- Implemented in the DAL (FFmpeg + file system) ---

public interface IMediaProbe
{
    Task<MediaInfo> ProbeAsync(string inputPath, CancellationToken ct = default);
}

public interface ITranscoder
{
    /// <summary>Encodes one rendition as HLS (index.m3u8 + 4-second .ts segments) into outputDirectory.</summary>
    /// <param name="onProgress">Called with 0..1 as encoding advances.</param>
    Task TranscodeToHlsAsync(string inputPath, string outputDirectory, RenditionPlan plan, double durationSeconds,
        Func<double, Task> onProgress, CancellationToken ct = default);
}

public interface IThumbnailExtractor
{
    Task ExtractAsync(string inputPath, string outputPath, double atSecond, CancellationToken ct = default);
}

public interface IMediaFiles
{
    /// <summary>Absolute path on disk for a path relative to the media root.</summary>
    string AbsolutePath(string relativePath);

    /// <summary>Creates an empty HLS folder for the video (removing output of an earlier attempt).</summary>
    HlsOutput PrepareHlsDirectory(string videoId);

    Task WriteTextAsync(string absolutePath, string content, CancellationToken ct = default);

    /// <summary>Removes partial HLS output after a failure.</summary>
    void DeleteHlsDirectory(string videoId);
}
