using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Processing.BLL.Interfaces;
using Processing.Entities;

namespace Processing.DAL.Ffmpeg;

/// <summary>Reads a file's duration, resolution and audio presence with ffprobe (JSON output).</summary>
public sealed class FfprobeMediaProbe(IOptions<FfmpegOptions> options) : IMediaProbe
{
    public async Task<MediaInfo> ProbeAsync(string inputPath, CancellationToken ct = default)
    {
        var result = await ProcessRunner.RunAsync(options.Value.FfprobePath,
            ["-v", "error", "-print_format", "json", "-show_format", "-show_streams", inputPath],
            onStdOutLine: null, ct, captureStdOut: true);

        if (result.ExitCode != 0)
            throw new InvalidOperationException($"ffprobe failed: {result.StdErrTail}");

        using var json = JsonDocument.Parse(result.StdOut);
        var root = json.RootElement;

        double duration = 0;
        if (root.TryGetProperty("format", out var format) && format.TryGetProperty("duration", out var d))
            double.TryParse(d.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out duration);

        int width = 0, height = 0;
        var hasAudio = false;
        foreach (var stream in root.GetProperty("streams").EnumerateArray())
        {
            var type = stream.GetProperty("codec_type").GetString();
            if (type == "audio") hasAudio = true;
            if (type != "video" || width > 0) continue;
            if (stream.TryGetProperty("disposition", out var disp) && disp.TryGetProperty("attached_pic", out var pic) && pic.GetInt32() == 1)
                continue; // cover art embedded in the file, not the actual video

            width = stream.GetProperty("width").GetInt32();
            height = stream.GetProperty("height").GetInt32();

            // Phones record landscape pixels plus a "rotate 90°" flag. FFmpeg auto-rotates when
            // encoding, so the ladder must be planned on the rotated (displayed) size.
            if (Math.Abs(Rotation(stream)) % 180 == 90)
                (width, height) = (height, width);
        }

        return new MediaInfo(duration, width, height, hasAudio);
    }

    private static int Rotation(JsonElement stream)
    {
        if (stream.TryGetProperty("side_data_list", out var sideData))
            foreach (var item in sideData.EnumerateArray())
                if (item.TryGetProperty("rotation", out var r) && r.ValueKind == JsonValueKind.Number)
                    return r.GetInt32();

        if (stream.TryGetProperty("tags", out var tags) && tags.TryGetProperty("rotate", out var rotate)
            && int.TryParse(rotate.GetString(), out var degrees))
            return degrees;

        return 0;
    }
}
