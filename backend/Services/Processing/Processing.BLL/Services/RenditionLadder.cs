using Processing.Entities;

namespace Processing.BLL.Services;

/// <summary>
/// The adaptive-bitrate "ladder": every quality we can offer, with its target bitrate.
/// A source only gets the rungs at or below its own resolution — upscaling a 480p upload to
/// "1080p" would cost bandwidth and storage without adding any detail.
/// </summary>
public static class RenditionLadder
{
    public static readonly RenditionProfile[] All =
    [
        new("1080p", 1080, 5000, 128),
        new("720p", 720, 2800, 128),
        new("480p", 480, 1400, 96),
        new("360p", 360, 800, 96),
        new("240p", 240, 400, 64),
        new("144p", 144, 200, 64)
    ];

    // Phones and some encoders produce sizes like 1072 or 718 — still treat those as the rung above.
    private const int Tolerance = 16;

    public static IReadOnlyList<RenditionPlan> For(int sourceWidth, int sourceHeight)
    {
        var landscape = sourceWidth >= sourceHeight;
        var shortSide = Math.Min(sourceWidth, sourceHeight);
        var longSide = Math.Max(sourceWidth, sourceHeight);

        var rungs = All.Where(r => r.ShortSide <= shortSide + Tolerance).ToList();
        if (rungs.Count == 0) rungs = [All[^1]]; // tiny source: still produce the smallest rung

        return [.. rungs.Select(r =>
        {
            // Keep the aspect ratio; H.264 needs even dimensions.
            var outShort = Even(r.ShortSide);
            var outLong = Even(r.ShortSide * (double)longSide / shortSide);
            return landscape ? new RenditionPlan(r, outLong, outShort) : new RenditionPlan(r, outShort, outLong);
        })];
    }

    private static int Even(double value) => (int)Math.Round(value / 2) * 2;
}
