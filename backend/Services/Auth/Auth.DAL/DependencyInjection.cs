using Auth.BLL.Interfaces;
using Auth.DAL.Data;
using Auth.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Auth.DAL;

public static class DependencyInjection
{
    /// <summary>Registers the data layer: the DbContext and the repositories the BLL asks for.</summary>
    public static IServiceCollection AddAuthDal(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AuthDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("AuthDb"),
                sql => sql.EnableRetryOnFailure())); // transient-fault retries: first taste of resilience

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AuthDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        return services;
    }
}
