using Microsoft.Extensions.DependencyInjection;
using User.BLL.Interfaces;
using User.BLL.Services;

namespace User.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddUserBll(this IServiceCollection services)
    {
        services.AddScoped<IPreferencesService, PreferencesService>();
        services.AddScoped<IProfileService, ProfileService>();
        return services;
    }
}
