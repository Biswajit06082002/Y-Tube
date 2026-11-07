using Comment.BLL.Interfaces;
using YTube.Common.Messaging;
using YTube.Contracts.Events;

namespace Comment.Api.Messaging;

/// <summary>Updates the author name/@handle stored on comments when a profile changes ("user.profile.updated").</summary>
public sealed class UserEventsConsumer(
    RabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    ILogger<UserEventsConsumer> logger) : RabbitMqConsumerService(connection, scopeFactory, logger)
{
    // Its own queue: the Video service's "video.user-events" queue gets its own copy of every event.
    protected override string QueueName => "comment.user-events";
    protected override string[] RoutingKeys => [UserRoutingKeys.ProfileUpdated];

    protected override Task HandleAsync(string routingKey, ReadOnlyMemory<byte> body, IServiceProvider services, CancellationToken ct) =>
        services.GetRequiredService<IAuthorSyncHandler>().HandleAsync(Deserialize<UserProfileUpdated>(body), ct);
}
