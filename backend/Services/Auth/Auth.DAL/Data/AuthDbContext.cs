using Auth.BLL.Interfaces;
using Auth.Entities;
using Microsoft.EntityFrameworkCore;

namespace Auth.DAL.Data;

/// <summary>
/// Auth owns its own database (AuthDb). No other service reads these tables directly —
/// "database per service" keeps services independently deployable.
/// </summary>
public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(user =>
        {
            user.ToTable("Users");
            user.HasKey(u => u.Id);
            user.Property(u => u.Email).HasMaxLength(256).IsRequired();
            user.HasIndex(u => u.Email).IsUnique();
            user.Property(u => u.PasswordHash).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<RefreshToken>(token =>
        {
            token.ToTable("RefreshTokens");
            token.HasKey(t => t.Id);
            token.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
            token.HasIndex(t => t.TokenHash).IsUnique();
            token.Property(t => t.ReplacedByTokenHash).HasMaxLength(64);
            token.Ignore(t => t.IsActive);
            token.HasOne(t => t.User)
                 .WithMany(u => u.RefreshTokens)
                 .HasForeignKey(t => t.UserId)
                 .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
