namespace Processing.Worker;

/// <summary>
/// Background worker with no HTTP API. In Phase 3 it will consume "VideoUploaded" events
/// from a message queue and transcode / generate thumbnails asynchronously.
/// </summary>
public class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Processing worker started; waiting for video jobs (not wired yet)");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            logger.LogDebug("Processing worker heartbeat at {Time}", DateTimeOffset.Now);
        }
    }
}
