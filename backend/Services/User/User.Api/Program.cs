using Microsoft.EntityFrameworkCore;
using User.Api.Messaging;
using User.BLL;
using User.DAL;
using User.DAL.Context;
using YTube.Common.Auth;
using YTube.Common.Caching;
using YTube.Common.Messaging;
using YTube.Common.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddServiceDefaults();
builder.Services.AddYTubeJwtAuthentication(builder.Configuration);
builder.Services.AddYTubeRedis(builder.Configuration);
builder.Services.AddYTubeRabbitMq(builder.Configuration);

builder.Services.AddUserBll();
builder.Services.AddUserDal(builder.Configuration);
builder.Services.AddHostedService<UserRegisteredConsumer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<UserDbContext>().Database.MigrateAsync();
}

app.UseServiceDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapServiceDefaults();

app.Run();
