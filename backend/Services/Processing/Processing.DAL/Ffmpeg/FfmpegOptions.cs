namespace Processing.DAL.Ffmpeg;

public sealed class FfmpegOptions
{
    public const string SectionName = "Ffmpeg";

    /// <summary>"ffmpeg" uses PATH; set a full path to pin a specific build.</summary>
    public string FfmpegPath { get; init; } = "ffmpeg";
    public string FfprobePath { get; init; } = "ffprobe";

    /// <summary>x264 speed/quality trade-off. "veryfast" keeps local encoding quick; production often uses "medium".</summary>
    public string Preset { get; init; } = "veryfast";

    /// <summary>HLS segment length. Quality switches happen at segment boundaries, so shorter = snappier switching.</summary>
    public int SegmentSeconds { get; init; } = 4;
}
