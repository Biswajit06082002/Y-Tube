using Microsoft.AspNetCore.Http;

namespace YTube.Common.Web;

/// <summary>
/// For service-to-service HTTP calls: copies the caller's Authorization and correlation id
/// onto the outgoing request, so the downstream service knows who the user is and the
/// whole chain shows up under one correlation id in the logs.
/// </summary>
public sealed class HeaderPropagationHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    private static readonly string[] Headers = ["Authorization", CorrelationIdMiddleware.HeaderName];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var incoming = accessor.HttpContext?.Request.Headers;
        if (incoming is not null)
        {
            foreach (var name in Headers)
            {
                var value = incoming[name].ToString();
                if (!string.IsNullOrEmpty(value) && !request.Headers.Contains(name))
                    request.Headers.TryAddWithoutValidation(name, value);
            }
        }

        return base.SendAsync(request, ct);
    }
}
