using Microsoft.Extensions.DependencyInjection;
using Video.BLL.Interfaces;
using Video.BLL.Services;

namespace Video.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddVideoBll(this IServiceCollection services)
    {
        services.AddScoped<IVideoService, VideoService>();
        services.AddScoped<IVideoProcessingEventsHandler, VideoProcessingEventsHandler>();
        services.AddScoped<IViewService, ViewService>();
        services.AddScoped<IChannelSyncHandler, ChannelSyncHandler>();
        return services;
    }
}
