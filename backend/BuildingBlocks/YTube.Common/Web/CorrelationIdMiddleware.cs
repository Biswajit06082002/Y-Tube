using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace YTube.Common.Web;

/// <summary>
/// Gives each request an id that travels gateway → service → service, so one user action
/// can be followed across every service's logs (first step towards distributed tracing).
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
            // Set on the request too, so a reverse proxy forwards it downstream.
            context.Request.Headers[HeaderName] = correlationId;
        }

        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }
}
