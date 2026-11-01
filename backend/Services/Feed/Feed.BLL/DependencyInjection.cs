using Feed.BLL.Interfaces;
using Feed.BLL.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Feed.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddFeedBll(this IServiceCollection services)
    {
        services.AddScoped<IFeedService, FeedService>();
        return services;
    }
}
