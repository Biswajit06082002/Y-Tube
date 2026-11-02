using System.Text.Json;
using RabbitMQ.Client;

namespace YTube.Common.Messaging;

public interface IMessagePublisher
{
    /// <summary>Publishes and waits until RabbitMQ confirms it stored the message (publisher confirms).</summary>
    Task PublishAsync<T>(string routingKey, T message, CancellationToken ct = default);
}

/// <summary>
/// Channels aren't thread-safe, so one channel is shared behind a lock. Messages are persistent
/// and the channel uses publisher confirms: when PublishAsync returns, the broker has the message.
/// </summary>
public sealed class RabbitMqPublisher(RabbitMqConnection connection) : IMessagePublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IChannel? _channel;

    public async Task PublishAsync<T>(string routingKey, T message, CancellationToken ct = default)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(message, JsonSerializerOptions.Web);
        var properties = new BasicProperties
        {
            Persistent = true, // survives a broker restart
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString("N"),
            Type = typeof(T).Name,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        await _lock.WaitAsync(ct);
        try
        {
            var channel = await GetChannelAsync(ct);
            await channel.BasicPublishAsync(connection.Options.Exchange, routingKey, mandatory: false, properties, body, ct);
        }
        catch
        {
            // Drop a broken channel so the next publish opens a fresh one.
            _channel = null;
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true }) return _channel;

        var conn = await connection.GetAsync(ct);
        _channel = await conn.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            ct);
        await connection.DeclareExchangesAsync(_channel, ct);
        return _channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
            await _channel.DisposeAsync();
        _lock.Dispose();
    }
}
