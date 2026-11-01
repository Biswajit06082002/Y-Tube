using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;

namespace YTube.Common.Web;

public static class ServiceDefaultsExtensions
{
    /// <summary>Things every HTTP service needs: error handling, health checks, OpenAPI.</summary>
    public static IServiceCollection AddServiceDefaults(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        services.AddHealthChecks();
        services.AddOpenApi();
        return services;
    }

    public static WebApplication UseServiceDefaults(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseMiddleware<CorrelationIdMiddleware>();
        return app;
    }

    public static WebApplication MapServiceDefaults(this WebApplication app)
    {
        // Liveness endpoint: load balancers / orchestrators poll this to know if the instance is up.
        app.MapHealthChecks("/health");

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference(); // API explorer UI at /scalar
        }

        return app;
    }
}
