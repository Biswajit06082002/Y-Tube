namespace YTube.Common.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string UserName { get; init; } = "guest";
    public string Password { get; init; } = "guest";

    /// <summary>Topic exchange every service publishes events to.</summary>
    public string Exchange { get; init; } = "ytube.events";

    /// <summary>Messages that fail are routed here (dead-letter exchange) instead of being lost or retried forever.</summary>
    public string DeadLetterExchange { get; init; } = "ytube.dlx";
}
