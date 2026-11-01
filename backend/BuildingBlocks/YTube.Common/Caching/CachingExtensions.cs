using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace YTube.Common.Caching;

public static class CachingExtensions
{
    public static IServiceCollection AddYTubeRedis(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Redis")
                               ?? throw new InvalidOperationException("Missing ConnectionStrings:Redis.");

        var options = ConfigurationOptions.Parse(connectionString);
        // Let the service start even when Redis is down; calls fail until it comes back
        // instead of the whole process crashing at startup.
        options.AbortOnConnectFail = false;

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(options));
        services.AddSingleton<ICacheService, RedisCacheService>();
        return services;
    }
}
