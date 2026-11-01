using Microsoft.Extensions.DependencyInjection;
using Video.BLL.Interfaces;
using Video.DAL.Repositories;

namespace Video.DAL;

public static class DependencyInjection
{
    public static IServiceCollection AddVideoDal(this IServiceCollection services)
    {
        // Singleton because the in-memory data must survive between requests.
        // A SQL repository (Phase 2) will be registered as Scoped instead.
        services.AddSingleton<IVideoRepository, InMemoryVideoRepository>();
        return services;
    }
}
