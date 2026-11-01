using Microsoft.EntityFrameworkCore;
using User.DAL.Models;

namespace User.DAL.Context;

/// <summary>The User service's own database (YTube_UserDb) — "database per service".</summary>
public sealed class UserDbContext(DbContextOptions<UserDbContext> options) : DbContext(options)
{
    public DbSet<TUserPreferences> Preferences => Set<TUserPreferences>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TUserPreferences>(prefs =>
        {
            prefs.ToTable("UserPreferences");
            prefs.HasKey(p => p.UserId);
            prefs.Property(p => p.Theme).HasMaxLength(32).IsRequired();
        });
    }
}
