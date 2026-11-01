using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using User.BLL.Interfaces;
using User.DAL.Data;
using User.DAL.Repositories;

namespace User.DAL;

public static class DependencyInjection
{
    public static IServiceCollection AddUserDal(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<UserDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("UserDb"), sql => sql.EnableRetryOnFailure()));
        services.AddScoped<IPreferencesRepository, PreferencesRepository>();
        return services;
    }
}
