using Feed.BLL.Interfaces;
using Feed.DAL.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YTube.Common.Web;

namespace Feed.DAL;

public static class DependencyInjection
{
    public static IServiceCollection AddFeedDal(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddTransient<HeaderPropagationHandler>();
        services.AddHttpClient<IVideoCatalogClient, VideoServiceClient>(client =>
            {
                client.BaseAddress = new Uri(configuration["Services:Video"]
                                             ?? throw new InvalidOperationException("Missing Services:Video."));
                client.Timeout = TimeSpan.FromSeconds(5); // never let a slow dependency hang our requests forever
            })
            .AddHttpMessageHandler<HeaderPropagationHandler>();

        services.AddHttpClient<IUserFollowsClient, UserServiceClient>(client =>
            {
                client.BaseAddress = new Uri(configuration["Services:User"]
                                             ?? throw new InvalidOperationException("Missing Services:User."));
                client.Timeout = TimeSpan.FromSeconds(5);
            })
            .AddHttpMessageHandler<HeaderPropagationHandler>();
        return services;
    }
}
