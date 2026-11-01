using Auth.Application.Abstractions;
using Auth.Infrastructure.Email;
using Auth.Infrastructure.Persistence;
using Auth.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Auth.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAuthInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AuthDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("AuthDb"),
                sql => sql.EnableRetryOnFailure())); // transient-fault retries: first taste of resilience

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AuthDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        var smtpSection = configuration.GetSection(SmtpOptions.SectionName);
        services.Configure<SmtpOptions>(smtpSection);
        if (string.IsNullOrWhiteSpace(smtpSection["Host"]))
            services.AddSingleton<IEmailSender, ConsoleEmailSender>();
        else
            services.AddSingleton<IEmailSender, SmtpEmailSender>();

        return services;
    }
}
