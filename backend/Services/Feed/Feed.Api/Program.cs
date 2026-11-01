using Feed.Api.Clients;
using YTube.Common.Auth;
using YTube.Common.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddServiceDefaults();
builder.Services.AddYTubeJwtAuthentication(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<HeaderPropagationHandler>();
builder.Services.AddHttpClient<VideoServiceClient>(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["Services:Video"]
                                     ?? throw new InvalidOperationException("Missing Services:Video."));
        client.Timeout = TimeSpan.FromSeconds(5); // never let a slow dependency hang our requests forever
    })
    .AddHttpMessageHandler<HeaderPropagationHandler>();

var app = builder.Build();

app.UseServiceDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapServiceDefaults();

app.Run();
