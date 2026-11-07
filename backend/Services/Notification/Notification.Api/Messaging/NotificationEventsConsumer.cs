using Notification.BLL.Interfaces;
using YTube.Common.Messaging;
using YTube.Contracts.Events;

namespace Notification.Api.Messaging;

/// <summary>One queue for every event that can notify someone. Producers don't know this service exists.</summary>
public sealed class NotificationEventsConsumer(
    RabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationEventsConsumer> logger) : RabbitMqConsumerService(connection, scopeFactory, logger)
{
    protected override string QueueName => "notification.events";

    protected override string[] RoutingKeys =>
    [
        YTube.Contracts.Events.RoutingKeys.VideoPublished,
        UserRoutingKeys.UserFollowed,
        CommentRoutingKeys.UserMentioned,
        CommentRoutingKeys.CommentCreated
    ];

    protected override Task HandleAsync(string routingKey, ReadOnlyMemory<byte> body, IServiceProvider services, CancellationToken ct)
    {
        var handler = services.GetRequiredService<INotificationEventsHandler>();
        return routingKey switch
        {
            YTube.Contracts.Events.RoutingKeys.VideoPublished => handler.HandleAsync(Deserialize<VideoPublished>(body), ct),
            UserRoutingKeys.UserFollowed => handler.HandleAsync(Deserialize<UserFollowed>(body), ct),
            CommentRoutingKeys.UserMentioned => handler.HandleAsync(Deserialize<UserMentioned>(body), ct),
            CommentRoutingKeys.CommentCreated => handler.HandleAsync(Deserialize<CommentCreated>(body), ct),
            _ => Task.CompletedTask
        };
    }
}
