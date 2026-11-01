using Auth.BLL.Interfaces;
using Auth.BLL.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Auth.BLL;

public static class DependencyInjection
{
    /// <summary>Registers the business layer: auth logic, JWT creation and email sending.</summary>
    public static IServiceCollection AddAuthBll(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAuthService, AuthService>();
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
