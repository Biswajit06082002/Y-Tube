using Video.BLL.Interfaces;
using YTube.Common.Messaging;
using YTube.Contracts.Events;

namespace Video.Api.Messaging;

/// <summary>
/// Listens for the Processing worker's events ("video.processing.*") and hands them to the BLL,
/// which updates the video's status. One queue, several event types, told apart by routing key.
/// </summary>
public sealed class ProcessingEventsConsumer(
    RabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    ILogger<ProcessingEventsConsumer> logger) : RabbitMqConsumerService(connection, scopeFactory, logger)
{
    protected override string QueueName => "video.processing-events";
    protected override string[] RoutingKeys => ["video.processing.*"];

    protected override Task HandleAsync(string routingKey, ReadOnlyMemory<byte> body, IServiceProvider services, CancellationToken ct)
    {
        var handler = services.GetRequiredService<IVideoProcessingEventsHandler>();
        return routingKey switch
        {
            YTube.Contracts.Events.RoutingKeys.ProcessingProgress => handler.HandleAsync(Deserialize<VideoProcessingProgress>(body), ct),
            YTube.Contracts.Events.RoutingKeys.ProcessingCompleted => handler.HandleAsync(Deserialize<VideoProcessingCompleted>(body), ct),
            YTube.Contracts.Events.RoutingKeys.ProcessingFailed => handler.HandleAsync(Deserialize<VideoProcessingFailed>(body), ct),
            _ => Task.CompletedTask // unknown event types are ignored (forward compatibility)
        };
    }
}
