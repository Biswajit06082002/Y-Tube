using Video.BLL.Interfaces;
using YTube.Common.Messaging;
using YTube.Contracts.Events;

namespace Video.Api.Messaging;

/// <summary>Updates the creator name stored on videos when a profile changes ("user.profile.updated").</summary>
public sealed class UserEventsConsumer(
    RabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    ILogger<UserEventsConsumer> logger) : RabbitMqConsumerService(connection, scopeFactory, logger)
{
    protected override string QueueName => "video.user-events";
    protected override string[] RoutingKeys => [UserRoutingKeys.ProfileUpdated];

    protected override Task HandleAsync(string routingKey, ReadOnlyMemory<byte> body, IServiceProvider services, CancellationToken ct) =>
        services.GetRequiredService<IChannelSyncHandler>().HandleAsync(Deserialize<UserProfileUpdated>(body), ct);
}
