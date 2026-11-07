using Comment.BLL.Interfaces;
using Comment.DAL.Context;
using Comment.DAL.Mappings;
using Comment.DAL.Models;
using Comment.Entities;
using Microsoft.EntityFrameworkCore;

namespace Comment.DAL.Repositories;

public sealed class CommentRepository(CommentDbContext db) : ICommentRepository
{
    public async Task<CommentItem?> GetAsync(long id, CancellationToken ct = default) =>
        (await db.Comments.AsNoTracking().Include(c => c.Mentions).FirstOrDefaultAsync(c => c.Id == id, ct))?.ToEntity();

    /// <summary>
    /// Keyset paging: "WHERE Id &lt; @cursor ORDER BY Id DESC". Unlike OFFSET, the database seeks
    /// straight to the cursor in the index, and new comments arriving don't shift the pages.
    /// </summary>
    public async Task<IReadOnlyList<CommentItem>> GetTopLevelAsync(string videoId, long? beforeId, int take, CancellationToken ct = default)
    {
        var query = db.Comments.AsNoTracking().Where(c => c.VideoId == videoId && c.ParentId == null);
        if (beforeId is { } before) query = query.Where(c => c.Id < before);

        var rows = await query.OrderByDescending(c => c.Id).Take(take).Include(c => c.Mentions).AsSplitQuery().ToListAsync(ct);
        return [.. rows.Select(c => c.ToEntity())];
    }

    public async Task<IReadOnlyList<CommentItem>> GetRepliesAsync(long parentId, long? afterId, int take, CancellationToken ct = default)
    {
        var query = db.Comments.AsNoTracking().Where(c => c.ParentId == parentId);
        if (afterId is { } after) query = query.Where(c => c.Id > after);

        var rows = await query.OrderBy(c => c.Id).Take(take).Include(c => c.Mentions).AsSplitQuery().ToListAsync(ct);
        return [.. rows.Select(c => c.ToEntity())];
    }

    public Task<int> CountAsync(string videoId, CancellationToken ct = default) =>
        db.Comments.CountAsync(c => c.VideoId == videoId, ct);

    public async Task AddAsync(CommentItem comment, CancellationToken ct = default)
    {
        var row = comment.ToModel();

        // With connection retries enabled, a transaction must run inside the execution strategy,
        // so a retry repeats the whole unit (insert + counter) instead of half of it.
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            db.Comments.Add(row);
            await db.SaveChangesAsync(ct);

            // Relative update ("+ 1"), so two replies at the same moment can't overwrite each other.
            if (row.ParentId is { } parentId)
                await db.Comments.Where(c => c.Id == parentId)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.ReplyCount, c => c.ReplyCount + 1), ct);

            await tx.CommitAsync(ct);
        });
        comment.Id = row.Id;
    }

    public async Task UpdateTextAsync(long id, string text, IReadOnlyList<CommentMention> mentions, DateTime editedAtUtc, CancellationToken ct = default)
    {
        var row = await db.Comments.Include(c => c.Mentions).FirstAsync(c => c.Id == id, ct);
        row.Text = text;
        row.EditedAtUtc = editedAtUtc;
        row.Mentions.Clear();
        row.Mentions.AddRange(mentions.Select(m => new TCommentMention { CommentId = id, UserId = m.UserId, Handle = m.Handle }));
        await db.SaveChangesAsync(ct);
    }

    public Task DeleteAsync(CommentItem comment, CancellationToken ct = default) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        if (comment.ParentId is { } parentId)
        {
            var deleted = await db.Comments.Where(c => c.Id == comment.Id).ExecuteDeleteAsync(ct);
            if (deleted > 0)
                await db.Comments.Where(c => c.Id == parentId && c.ReplyCount > 0)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.ReplyCount, c => c.ReplyCount - 1), ct);
        }
        else
        {
            await db.Comments.Where(c => c.ParentId == comment.Id).ExecuteDeleteAsync(ct); // replies first (FK)
            await db.Comments.Where(c => c.Id == comment.Id).ExecuteDeleteAsync(ct);
        }

        await tx.CommitAsync(ct);
    });

    public Task RenameAuthorAsync(Guid authorId, string name, string handle, CancellationToken ct = default) =>
        db.Comments.Where(c => c.AuthorId == authorId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.AuthorName, name).SetProperty(c => c.AuthorHandle, handle), ct);
}
