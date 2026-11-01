using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using YTube.Common.Responses;

namespace YTube.Common.Web;

/// <summary>
/// Last line of defense: logs unexpected exceptions and returns a clean 500,
/// instead of swallowing errors in empty catch blocks.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(
            ApiResponse<object>.Fail("server_error", $"Something went wrong. Reference: {context.TraceIdentifier}"), ct);
        return true;
    }
}
