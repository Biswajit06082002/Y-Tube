using Processing.BLL.Interfaces;
using YTube.Common.Messaging;
using YTube.Contracts.Events;

namespace Processing.Worker.Messaging;

/// <summary>
/// Pulls "video.uploaded" jobs off the queue. Prefetch = 1: each worker transcodes one video at a
/// time (it's CPU-heavy). To process more in parallel, start more worker instances — RabbitMQ
/// hands each job to exactly one of them (the "competing consumers" pattern).
/// </summary>
public sealed class VideoUploadedConsumer(
    RabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    ILogger<VideoUploadedConsumer> logger) : RabbitMqConsumerService(connection, scopeFactory, logger)
{
    protected override string QueueName => "processing.video-uploaded";
    protected override string[] RoutingKeys => [YTube.Contracts.Events.RoutingKeys.VideoUploaded];
    protected override ushort Prefetch => 1;

    protected override Task HandleAsync(string routingKey, ReadOnlyMemory<byte> body, IServiceProvider services, CancellationToken ct) =>
        services.GetRequiredService<IVideoProcessor>().ProcessAsync(Deserialize<VideoUploaded>(body), ct);
}
