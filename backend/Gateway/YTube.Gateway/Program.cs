using System.Threading.RateLimiting;
using YTube.Common.Auth;
using YTube.Common.Web;

var builder = WebApplication.CreateBuilder(args);

// Kestrel rejects bodies over 30 MB by default. Video uploads go through the gateway, so allow
// up to ~2.1 GB here; the Video service enforces the real per-endpoint limit ([RequestSizeLimit]).
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 2_200L * 1024 * 1024);

// Edge authentication: requests to protected routes without a valid JWT never reach a service.
builder.Services.AddYTubeJwtAuthentication(builder.Configuration);
builder.Services.AddHealthChecks();

builder.Services.AddCors(options =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(origins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials() // lets the browser send/receive the refresh-token cookie cross-origin (4200 → 5000)
        .WithExposedHeaders(CorrelationIdMiddleware.HeaderName));
});

// Rate limiting protects services from abuse and brute force. Partitioned per client IP.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global: token bucket allows short bursts but caps the sustained rate.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetTokenBucketLimiter(ClientIp(context), _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 100,
            TokensPerPeriod = 50,
            ReplenishmentPeriod = TimeSpan.FromSeconds(10),
            QueueLimit = 0
        }));

    // Auth endpoints are a brute-force target (passwords, OTPs), so they get a much tighter window.
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(ClientIp(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    // Comment writes: generous for people, but stops a script from flooding a video with spam.
    options.AddPolicy("comments", context =>
        RateLimitPartition.GetFixedWindowLimiter(ClientIp(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapReverseProxy();

app.Run();

static string ClientIp(HttpContext context) =>
    context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
