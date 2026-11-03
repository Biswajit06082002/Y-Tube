using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using User.DAL.Models;

namespace User.DAL.Context;

/// <summary>The User service's own database (YTube_UserDb) — "database per service".</summary>
public sealed class UserDbContext(DbContextOptions<UserDbContext> options) : DbContext(options)
{
    public DbSet<TUserPreferences> Preferences => Set<TUserPreferences>();
    public DbSet<TUserProfile> Profiles => Set<TUserProfile>();
    public DbSet<TFollow> Follows => Set<TFollow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TUserPreferences>(prefs =>
        {
            prefs.ToTable("UserPreferences");
            prefs.HasKey(p => p.UserId);
            prefs.Property(p => p.Theme).HasMaxLength(32).IsRequired();
        });

        modelBuilder.Entity<TUserProfile>(profile =>
        {
            profile.ToTable("UserProfiles");
            profile.HasKey(p => p.UserId);
            profile.Property(p => p.Handle).HasMaxLength(30).IsUnicode(false).IsRequired();
            profile.HasIndex(p => p.Handle).IsUnique(); // the database guarantees no two users share a handle
            profile.Property(p => p.DisplayName).HasMaxLength(50).IsRequired();
            profile.Property(p => p.Bio).HasMaxLength(1000);
        });

        modelBuilder.Entity<TFollow>(follow =>
        {
            follow.ToTable("Follows");
            follow.HasKey(f => new { f.FollowerId, f.FolloweeId }); // "who do I follow" uses this index
            follow.HasIndex(f => f.FolloweeId);                     // "who follows me" / follower count
        });
    }

    /// <summary>datetime2 has no time zone: mark values as UTC when reading so they serialise with "Z".</summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
