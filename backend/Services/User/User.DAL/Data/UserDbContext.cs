using Microsoft.EntityFrameworkCore;
using User.Entities;

namespace User.DAL.Data;

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
