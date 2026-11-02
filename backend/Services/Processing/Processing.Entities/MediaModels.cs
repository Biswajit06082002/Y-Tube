namespace Processing.Entities;

/// <summary>
/// One rung of the encoding ladder. ShortSide is the "p" number: 720 means 1280×720 for a
/// landscape video and 720×1280 for a portrait one.
/// </summary>
public sealed record RenditionProfile(string Name, int ShortSide, int VideoKbps, int AudioKbps);

/// <summary>A rendition to produce for a specific source: the profile plus its exact output size.</summary>
public sealed record RenditionPlan(RenditionProfile Profile, int Width, int Height);

/// <summary>What ffprobe found in the uploaded file (width/height already corrected for phone rotation).</summary>
public sealed record MediaInfo(double DurationSeconds, int Width, int Height, bool HasAudio);

/// <summary>Where a video's HLS output goes: absolute path for writing, relative path for the database/URLs.</summary>
public sealed record HlsOutput(string AbsoluteDirectory, string RelativeDirectory);
