using Microsoft.Extensions.DependencyInjection;
using Notification.BLL.Interfaces;
using Notification.BLL.Services;

namespace Notification.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationBll(this IServiceCollection services)
    {
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationEventsHandler, NotificationEventsHandler>();
        return services;
    }
}
