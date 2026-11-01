using Microsoft.Extensions.DependencyInjection;
using Video.BLL.Interfaces;
using Video.DAL.Context;
using Video.DAL.Repositories;

namespace Video.DAL;

public static class DependencyInjection
{
    public static IServiceCollection AddVideoDal(this IServiceCollection services)
    {
        // The in-memory context is a singleton so its data survives between requests
        // (an EF DbContext in Phase 2 will be scoped instead). Repositories are scoped, like with EF.
        services.AddSingleton<InMemoryVideoContext>();
        services.AddScoped<IVideoRepository, VideoRepository>();
        return services;
    }
}
