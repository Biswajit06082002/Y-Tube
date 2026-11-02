using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace YTube.Common.Messaging;

public static class MessagingExtensions
{
    public static IServiceCollection AddYTubeRabbitMq(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
        return services;
    }
}
