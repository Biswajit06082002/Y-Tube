using Auth.BLL;
using Auth.DAL;
using Auth.DAL.Data;
using Microsoft.EntityFrameworkCore;
using YTube.Common.Auth;
using YTube.Common.Caching;
using YTube.Common.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddServiceDefaults();
builder.Services.AddYTubeJwtAuthentication(builder.Configuration);
builder.Services.AddYTubeRedis(builder.Configuration);
builder.Services.AddAuthBll(builder.Configuration);
builder.Services.AddAuthDal(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Dev convenience: create/upgrade AuthDb on startup. In production, migrations run as a deploy step.
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.MigrateAsync();
}

app.UseServiceDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapServiceDefaults();

app.Run();
