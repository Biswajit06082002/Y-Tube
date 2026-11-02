using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Processing.BLL.Interfaces;
using Processing.DAL.Ffmpeg;
using Processing.DAL.Storage;

namespace Processing.DAL;

public static class DependencyInjection
{
    public static IServiceCollection AddProcessingDal(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FfmpegOptions>(configuration.GetSection(FfmpegOptions.SectionName));
        services.Configure<MediaStorageOptions>(configuration.GetSection(MediaStorageOptions.SectionName));

        services.AddSingleton<IMediaProbe, FfprobeMediaProbe>();
        services.AddSingleton<ITranscoder, FfmpegTranscoder>();
        services.AddSingleton<IThumbnailExtractor, FfmpegThumbnailExtractor>();
        services.AddSingleton<IMediaFiles, LocalMediaFiles>();
        return services;
    }
}
