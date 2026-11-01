using Microsoft.EntityFrameworkCore;
using User.Api.Data;
using User.Api.Preferences;
using YTube.Common.Auth;
using YTube.Common.Caching;
using YTube.Common.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddServiceDefaults();
builder.Services.AddYTubeJwtAuthentication(builder.Configuration);
builder.Services.AddYTubeRedis(builder.Configuration);

builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("UserDb"), sql => sql.EnableRetryOnFailure()));
builder.Services.AddScoped<PreferencesService>();

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
