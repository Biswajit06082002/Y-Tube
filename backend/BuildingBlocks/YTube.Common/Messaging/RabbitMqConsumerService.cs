using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace YTube.Common.Messaging;

/// <summary>
/// Base class for a background consumer of one queue.
///  - Manual acks: a message is removed from the queue only after HandleAsync succeeds.
///    If the process crashes mid-way, RabbitMQ redelivers it to another consumer.
///  - Failure → nack without requeue → the message goes to "{queue}.dlq" (dead-letter queue)
///    for inspection, instead of being retried in a tight loop forever.
///  - Prefetch limits how many unacked messages this consumer holds at once (back-pressure).
/// </summary>
public abstract class RabbitMqConsumerService(
    RabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    ILogger logger) : BackgroundService
{
    protected abstract string QueueName { get; }
    protected abstract string[] RoutingKeys { get; }
    protected virtual ushort Prefetch => 10;

    /// <summary>Handles one message inside its own DI scope (so scoped services like a DbContext are fresh per message).</summary>
    protected abstract Task HandleAsync(string routingKey, ReadOnlyMemory<byte> body, IServiceProvider services, CancellationToken ct);

    protected static T Deserialize<T>(ReadOnlyMemory<byte> body) =>
        JsonSerializer.Deserialize<T>(body.Span, JsonSerializerOptions.Web)
        ?? throw new InvalidOperationException($"Empty {typeof(T).Name} message.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Keep trying until RabbitMQ is reachable (e.g. the container is still starting).
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Could not start consuming {Queue}; retrying in 5s", QueueName);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        var conn = await connection.GetAsync(stoppingToken);
        var channel = await conn.CreateChannelAsync(cancellationToken: stoppingToken);
        var options = connection.Options;

        await connection.DeclareExchangesAsync(channel, stoppingToken);

        // Dead-letter queue first, then the main queue pointing failed messages at it.
        var dlq = $"{QueueName}.dlq";
        await channel.QueueDeclareAsync(dlq, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
        await channel.QueueBindAsync(dlq, options.DeadLetterExchange, routingKey: QueueName, cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = options.DeadLetterExchange,
                ["x-dead-letter-routing-key"] = QueueName
            },
            cancellationToken: stoppingToken);

        foreach (var key in RoutingKeys)
            await channel.QueueBindAsync(QueueName, options.Exchange, key, cancellationToken: stoppingToken);

        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: Prefetch, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await HandleAsync(delivery.RoutingKey, delivery.Body, scope.ServiceProvider, stoppingToken);
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutting down mid-job isn't the message's fault: put it back for the next consumer.
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to handle {RoutingKey} from {Queue}; sending to {Dlq}", delivery.RoutingKey, QueueName, dlq);
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, CancellationToken.None);
            }
        };

        await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer, stoppingToken);
        logger.LogInformation("Consuming {Queue} (bindings: {Keys})", QueueName, string.Join(", ", RoutingKeys));

        // Stay alive until shutdown; the consumer callbacks do the work.
        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, CancellationToken.None);
        await channel.CloseAsync(CancellationToken.None);
    }
}
