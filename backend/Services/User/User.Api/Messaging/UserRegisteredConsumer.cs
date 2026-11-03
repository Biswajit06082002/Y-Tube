using User.BLL.Interfaces;
using YTube.Common.Messaging;
using YTube.Contracts.Events;

namespace User.Api.Messaging;

/// <summary>Creates a profile as soon as Auth reports a new account ("user.registered").</summary>
public sealed class UserRegisteredConsumer(
    RabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    ILogger<UserRegisteredConsumer> logger) : RabbitMqConsumerService(connection, scopeFactory, logger)
{
    protected override string QueueName => "user.registrations";
    protected override string[] RoutingKeys => [UserRoutingKeys.UserRegistered];

    protected override Task HandleAsync(string routingKey, ReadOnlyMemory<byte> body, IServiceProvider services, CancellationToken ct)
    {
        var e = Deserialize<UserRegistered>(body);
        return services.GetRequiredService<IProfileService>().EnsureProfileAsync(e.UserId, e.Email, e.RegisteredAtUtc, ct);
    }
}
