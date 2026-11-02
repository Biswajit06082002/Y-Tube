using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Video.DAL.Models;

namespace Video.DAL.Context;

/// <summary>The Video service's own database (YTube_VideoDb) — "database per service".</summary>
public sealed class VideoDbContext(DbContextOptions<VideoDbContext> options) : DbContext(options)
{
    public DbSet<TVideo> Videos => Set<TVideo>();
    public DbSet<TVideoTag> VideoTags => Set<TVideoTag>();
    public DbSet<TVideoLike> VideoLikes => Set<TVideoLike>();

    /// <summary>
    /// SQL Server's datetime2 has no time zone, so EF reads values back as DateTimeKind.Unspecified,
    /// which serialise without a "Z" and browsers then treat as *local* time (an upload in India
    /// showed "5 hours ago"). Everything we store is UTC, so mark it as UTC when reading.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private sealed class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
        v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TVideo>(video =>
        {
            video.ToTable("Videos");
            video.HasKey(v => v.Id);
            video.Property(v => v.Id).HasMaxLength(11).IsUnicode(false);
            video.Property(v => v.Title).HasMaxLength(100).IsRequired();
            video.Property(v => v.Description).HasMaxLength(5000);
            video.Property(v => v.Category).HasMaxLength(40);
            video.Property(v => v.ChannelId).HasMaxLength(64);
            video.Property(v => v.ChannelName).HasMaxLength(100);
            video.Property(v => v.ProcessingError).HasMaxLength(500);
            video.Property(v => v.Renditions).HasMaxLength(100);
            video.Property(v => v.SourcePath).HasMaxLength(260);
            video.Property(v => v.ThumbnailPath).HasMaxLength(260);
            video.Property(v => v.MasterPlaylistPath).HasMaxLength(260);

            // Indexes follow the queries: the public feed (ready, newest first, optional category)
            // and "my uploads".
            video.HasIndex(v => new { v.Status, v.PublishedAtUtc });
            video.HasIndex(v => new { v.Status, v.Category, v.PublishedAtUtc });
            video.HasIndex(v => v.OwnerId);

            video.HasMany(v => v.Tags).WithOne(t => t.Video).HasForeignKey(t => t.VideoId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TVideoTag>(tag =>
        {
            tag.ToTable("VideoTags");
            tag.HasKey(t => new { t.VideoId, t.Tag });
            tag.Property(t => t.VideoId).HasMaxLength(11).IsUnicode(false);
            tag.Property(t => t.Tag).HasMaxLength(30);
            tag.HasIndex(t => t.Tag); // "videos with tag X" — for search / related videos later
        });

        modelBuilder.Entity<TVideoLike>(like =>
        {
            like.ToTable("VideoLikes");
            like.HasKey(l => new { l.VideoId, l.UserId });
            like.Property(l => l.VideoId).HasMaxLength(11).IsUnicode(false);
            like.Property(l => l.UserId).HasMaxLength(36).IsUnicode(false);
            like.HasIndex(l => l.UserId); // "videos I liked"
        });
    }
}
