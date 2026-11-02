using System.Globalization;
using Microsoft.Extensions.Options;
using Processing.BLL.Interfaces;

namespace Processing.DAL.Ffmpeg;

/// <summary>Grabs one frame as a 1280px-wide JPEG.</summary>
public sealed class FfmpegThumbnailExtractor(IOptions<FfmpegOptions> options) : IThumbnailExtractor
{
    public async Task ExtractAsync(string inputPath, string outputPath, double atSecond, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var result = await ProcessRunner.RunAsync(options.Value.FfmpegPath,
        [
            "-hide_banner", "-y",
            "-ss", atSecond.ToString("0.###", CultureInfo.InvariantCulture), // seek before -i = fast seek
            "-i", inputPath,
            "-frames:v", "1",
            "-vf", "scale=1280:-2",
            "-q:v", "3",
            outputPath
        ], onStdOutLine: null, ct);

        if (result.ExitCode != 0 || !File.Exists(outputPath))
            throw new InvalidOperationException($"Thumbnail extraction failed: {result.StdErrTail}");
    }
}
