using Comment.DAL.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Comment.DAL.Context;

/// <summary>The Comment service's own database (YTube_CommentDb) — "database per service".</summary>
public sealed class CommentDbContext(DbContextOptions<CommentDbContext> options) : DbContext(options)
{
    public DbSet<TComment> Comments => Set<TComment>();
    public DbSet<TCommentMention> Mentions => Set<TCommentMention>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TComment>(comment =>
        {
            comment.ToTable("Comments");
            comment.HasKey(c => c.Id);
            comment.Property(c => c.Id).UseIdentityColumn(); // increasing ids → cursor paging
            comment.Property(c => c.VideoId).HasMaxLength(64).IsUnicode(false).IsRequired();
            comment.Property(c => c.AuthorName).HasMaxLength(50).IsRequired();
            comment.Property(c => c.AuthorHandle).HasMaxLength(30).IsUnicode(false).IsRequired();
            comment.Property(c => c.Text).HasMaxLength(2000).IsRequired();

            // "Comments of a video, newest first" and "replies of a comment" both seek straight into this index.
            comment.HasIndex(c => new { c.VideoId, c.ParentId, c.Id });
            comment.HasIndex(c => c.ParentId);
            comment.HasIndex(c => c.AuthorId); // renaming an author

            // SQL Server won't cascade a self-reference, so the repository deletes replies itself.
            comment.HasOne<TComment>().WithMany().HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);
            comment.HasMany(c => c.Mentions).WithOne().HasForeignKey(m => m.CommentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TCommentMention>(mention =>
        {
            mention.ToTable("CommentMentions");
            mention.HasKey(m => new { m.CommentId, m.UserId });
            mention.HasIndex(m => m.UserId); // "comments that mention me" (notifications, later)
            mention.Property(m => m.Handle).HasMaxLength(30).IsUnicode(false).IsRequired();
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
