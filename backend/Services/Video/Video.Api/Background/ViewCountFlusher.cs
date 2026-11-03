using Video.BLL.Interfaces;

namespace Video.Api.Background;

/// <summary>Every 10 seconds, writes the views buffered in Redis to SQL (see ViewService).</summary>
public sealed class ViewCountFlusher(IServiceScopeFactory scopeFactory, ILogger<ViewCountFlusher> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IViewService>().FlushPendingAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Flushing view counts failed; will retry on the next tick");
            }
        }
    }
}
