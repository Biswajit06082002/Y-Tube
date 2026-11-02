using Microsoft.Extensions.DependencyInjection;
using Processing.BLL.Interfaces;
using Processing.BLL.Services;

namespace Processing.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddProcessingBll(this IServiceCollection services)
    {
        services.AddScoped<IVideoProcessor, VideoProcessor>();
        return services;
    }
}
