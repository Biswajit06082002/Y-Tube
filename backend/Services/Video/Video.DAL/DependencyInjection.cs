using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Video.BLL.Interfaces;
using Video.DAL.Context;
using Video.DAL.Repositories;
using Video.DAL.Storage;

namespace Video.DAL;

public static class DependencyInjection
{
    public static IServiceCollection AddVideoDal(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<VideoDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("VideoDb"), sql => sql.EnableRetryOnFailure()));
        services.AddScoped<IVideoRepository, VideoRepository>();

        services.Configure<MediaStorageOptions>(configuration.GetSection(MediaStorageOptions.SectionName));
        services.AddSingleton<IMediaStorage, LocalMediaStorage>();
        return services;
    }
}
