using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Video.Api.Background;
using Video.Api.Messaging;
using Video.BLL;
using Video.DAL;
using Video.DAL.Context;
using YTube.Common.Auth;
using YTube.Common.Caching;
using YTube.Common.Messaging;
using YTube.Common.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter())); // "Ready", not 2
builder.Services.AddServiceDefaults();
builder.Services.AddYTubeJwtAuthentication(builder.Configuration);
builder.Services.AddYTubeRabbitMq(builder.Configuration);
builder.Services.AddYTubeRedis(builder.Configuration);
builder.Services.AddVideoBll();
builder.Services.AddVideoDal(builder.Configuration);
builder.Services.AddHostedService<ProcessingEventsConsumer>();
builder.Services.AddHostedService<UserEventsConsumer>();
builder.Services.AddHostedService<ViewCountFlusher>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await VideoDbInitializer.InitializeAsync(scope.ServiceProvider.GetRequiredService<VideoDbContext>());
}

// Trust X-Forwarded-For from the gateway (loopback is a known proxy by default) so
// RemoteIpAddress is the real client — used to tell anonymous viewers apart.
app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor });
app.UseServiceDefaults();
UseMediaFiles(app);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapServiceDefaults();

app.Run();

// Serves {media root}/videos/** at /media/videos/** — HLS playlists, segments and thumbnails.
// Only the "videos" folder is exposed; original uploads are never public.
static void UseMediaFiles(WebApplication app)
{
    var root = app.Configuration["Storage:RootPath"]
               ?? throw new InvalidOperationException("Missing Storage:RootPath.");
    var videosDir = Path.Combine(root, "videos");
    Directory.CreateDirectory(videosDir);

    var contentTypes = new FileExtensionContentTypeProvider();
    contentTypes.Mappings[".m3u8"] = "application/vnd.apple.mpegurl";
    contentTypes.Mappings[".ts"] = "video/mp2t";

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(videosDir),
        RequestPath = "/media/videos",
        ContentTypeProvider = contentTypes,
        OnPrepareResponse = ctx =>
        {
            // Segments never change once written → cache "forever" (what a CDN would do).
            // Playlists and thumbnails can be replaced → short cache.
            var ext = Path.GetExtension(ctx.File.Name);
            ctx.Context.Response.Headers.CacheControl = ext == ".ts"
                ? "public, max-age=31536000, immutable"
                : "public, max-age=60";
        }
    });
}
