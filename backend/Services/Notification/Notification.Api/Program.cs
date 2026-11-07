using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Notification.Api.Hubs;
using Notification.Api.Messaging;
using Notification.BLL;
using Notification.BLL.Interfaces;
using Notification.DAL;
using Notification.DAL.Context;
using StackExchange.Redis;
using YTube.Common.Auth;
using YTube.Common.Messaging;
using YTube.Common.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddServiceDefaults();
builder.Services.AddYTubeJwtAuthentication(builder.Configuration);
builder.Services.AddYTubeRabbitMq(builder.Configuration);

// SignalR + Redis backplane: with several instances of this service, a user's browser is connected to
// only one of them. The backplane relays every "send to user X" through Redis pub/sub so whichever
// instance holds X's connection delivers it.
builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis")!,
        options => options.Configuration.ChannelPrefix = RedisChannel.Literal("ytube-signalr"));
builder.Services.AddSingleton<IUserIdProvider, SubClaimUserIdProvider>();
builder.Services.AddScoped<INotificationPusher, SignalRNotificationPusher>();

builder.Services.AddNotificationBll();
builder.Services.AddNotificationDal(builder.Configuration);
builder.Services.AddHostedService<NotificationEventsConsumer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database.MigrateAsync();
}

app.UseServiceDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationsHub>(NotificationsHub.Path);
app.MapServiceDefaults();

app.Run();
