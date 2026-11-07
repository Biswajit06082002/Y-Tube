using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Notification.DAL.Models;

namespace Notification.DAL.Context;

/// <summary>The Notification service's own database (YTube_NotificationDb) — "database per service".</summary>
public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<TNotification> Notifications => Set<TNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TNotification>(n =>
        {
            n.ToTable("Notifications");
            n.HasKey(x => x.Id);
            n.Property(x => x.Id).UseIdentityColumn(); // increasing ids → cursor paging
            n.Property(x => x.Type).HasMaxLength(20).IsUnicode(false).IsRequired(); // stored as text: readable in SQL
            n.Property(x => x.ActorName).HasMaxLength(50).IsRequired();
            n.Property(x => x.Text).HasMaxLength(300).IsRequired();
            n.Property(x => x.Snippet).HasMaxLength(300);
            n.Property(x => x.Link).HasMaxLength(200).IsUnicode(false).IsRequired();
            n.Property(x => x.ImageUrl).HasMaxLength(300).IsUnicode(false);
            n.Property(x => x.DedupKey).HasMaxLength(80).IsUnicode(false).IsRequired();

            // Idempotency: the same event can't notify the same person twice, even if RabbitMQ redelivers it.
            n.HasIndex(x => new { x.UserId, x.DedupKey }).IsUnique();

            // "My notifications, newest first" seeks straight into this index.
            n.HasIndex(x => new { x.UserId, x.Id });

            // The bell's badge asks "how many unread?" constantly. A filtered index holds only unread rows,
            // so it stays tiny however many old notifications pile up.
            n.HasIndex(x => x.UserId).HasFilter("[IsRead] = 0").HasDatabaseName("IX_Notifications_Unread");
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
