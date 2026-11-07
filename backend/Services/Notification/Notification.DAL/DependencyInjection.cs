using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notification.BLL.Interfaces;
using Notification.DAL.Clients;
using Notification.DAL.Context;
using Notification.DAL.Repositories;

namespace Notification.DAL;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationDal(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<NotificationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("NotificationDb"), sql => sql.EnableRetryOnFailure()));
        services.AddScoped<INotificationRepository, NotificationRepository>();

        // Called from a RabbitMQ consumer (no incoming request), so there's no user token to forward.
        services.AddHttpClient<IFollowersClient, UserServiceClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["Services:User"]
                                         ?? throw new InvalidOperationException("Missing Services:User."));
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        return services;
    }
}
