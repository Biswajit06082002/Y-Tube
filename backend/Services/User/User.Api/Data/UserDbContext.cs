using Microsoft.EntityFrameworkCore;

namespace User.Api.Data;

/// <summary>
/// Per-user settings. Keyed by the user id from the JWT ("sub") — the User service never reads
/// Auth's tables; the id in the token is the only link between the two databases.
/// </summary>
public class UserPreferences
{
    public Guid UserId { get; set; }
    public string Theme { get; set; } = "system";
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>The User service's own database (YTube_UserDb) — "database per service".</summary>
public sealed class UserDbContext(DbContextOptions<UserDbContext> options) : DbContext(options)
{
    public DbSet<UserPreferences> Preferences => Set<UserPreferences>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserPreferences>(prefs =>
        {
            prefs.ToTable("UserPreferences");
            prefs.HasKey(p => p.UserId);
            prefs.Property(p => p.Theme).HasMaxLength(32).IsRequired();
        });
    }
}
