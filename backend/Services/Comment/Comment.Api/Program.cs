using Comment.Api.Messaging;
using Comment.BLL;
using Comment.DAL;
using Comment.DAL.Context;
using Microsoft.EntityFrameworkCore;
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

builder.Services.AddCommentBll();
builder.Services.AddCommentDal(builder.Configuration);
builder.Services.AddHostedService<UserEventsConsumer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<CommentDbContext>().Database.MigrateAsync();
}

app.UseServiceDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapServiceDefaults();

app.Run();
