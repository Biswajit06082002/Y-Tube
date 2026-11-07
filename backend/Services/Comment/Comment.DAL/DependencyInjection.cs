using Comment.BLL.Interfaces;
using Comment.DAL.Clients;
using Comment.DAL.Context;
using Comment.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YTube.Common.Web;

namespace Comment.DAL;

public static class DependencyInjection
{
    public static IServiceCollection AddCommentDal(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CommentDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("CommentDb"), sql => sql.EnableRetryOnFailure()));
        services.AddScoped<ICommentRepository, CommentRepository>();

        services.AddHttpContextAccessor();
        services.AddTransient<HeaderPropagationHandler>();
        services.AddHttpClient<IVideoClient, VideoServiceClient>(client =>
            {
                client.BaseAddress = new Uri(configuration["Services:Video"]
                                             ?? throw new InvalidOperationException("Missing Services:Video."));
                client.Timeout = TimeSpan.FromSeconds(5); // never let a slow dependency hang our requests forever
            })
            .AddHttpMessageHandler<HeaderPropagationHandler>();

        services.AddHttpClient<IUserClient, UserServiceClient>(client =>
            {
                client.BaseAddress = new Uri(configuration["Services:User"]
                                             ?? throw new InvalidOperationException("Missing Services:User."));
                client.Timeout = TimeSpan.FromSeconds(5);
            })
            .AddHttpMessageHandler<HeaderPropagationHandler>();
        return services;
    }
}
