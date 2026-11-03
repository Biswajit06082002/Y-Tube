using Microsoft.Extensions.Logging;
using Video.BLL.Interfaces;
using Video.Entities;
using Video.Entities.Contracts;
using YTube.Common.Results;

namespace Video.BLL.Services;

/// <summary>
/// Counting views without hammering the database:
///  1. Dedup — the same viewer watching the same video again within 30 minutes isn't a new view
///     (stops refresh-spam). Redis SET NX with a TTL does the check atomically.
///  2. Buffer — a counted view is an INCR in Redis (microseconds), not a SQL UPDATE.
///  3. Flush — every few seconds the buffered counts are written to SQL in one batch.
/// A popular video then costs one UPDATE every few seconds instead of one per viewer
/// (no hot-row lock contention). Trade-off: counts in the database lag a few seconds behind,
/// and a crash can lose the last few seconds of views — fine for a view counter, not for money.
/// </summary>
public sealed class ViewService(
    IVideoRepository repository,
    IViewCounterStore counter,
    ILogger<ViewService> logger) : IViewService
{
    private static readonly TimeSpan DedupWindow = TimeSpan.FromMinutes(30);

    public async Task<Result<ViewResultDto>> RecordViewAsync(string videoId, string viewerKey, CancellationToken ct = default)
    {
        var video = await repository.GetByIdAsync(videoId, ct);
        if (video is null || video.Status != VideoStatus.Ready)
            return Error.NotFound("video_not_found", "This video isn't available.");

        if (!await counter.TryMarkViewedAsync(videoId, viewerKey, DedupWindow, ct))
            return new ViewResultDto(Counted: false);

        await counter.IncrementPendingAsync(videoId, ct);
        return new ViewResultDto(Counted: true);
    }

    public async Task<int> FlushPendingAsync(CancellationToken ct = default)
    {
        var pending = await counter.TakePendingAsync(ct);
        if (pending.Count == 0) return 0;

        await repository.AddViewsAsync(pending, ct);
        var total = (int)pending.Values.Sum();
        logger.LogInformation("Flushed {Views} views for {Videos} videos", total, pending.Count);
        return total;
    }
}
