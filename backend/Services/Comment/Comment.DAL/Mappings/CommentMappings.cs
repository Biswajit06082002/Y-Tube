using Comment.DAL.Models;
using Comment.Entities;

namespace Comment.DAL.Mappings;

/// <summary>Converts between table models (DAL) and business entities (BLL).</summary>
internal static class CommentMappings
{
    public static CommentItem ToEntity(this TComment m) => new()
    {
        Id = m.Id,
        VideoId = m.VideoId,
        ParentId = m.ParentId,
        AuthorId = m.AuthorId,
        AuthorName = m.AuthorName,
        AuthorHandle = m.AuthorHandle,
        Text = m.Text,
        ReplyCount = m.ReplyCount,
        CreatedAtUtc = m.CreatedAtUtc,
        EditedAtUtc = m.EditedAtUtc,
        Mentions = [.. m.Mentions.Select(x => new CommentMention(x.UserId, x.Handle))]
    };

    public static TComment ToModel(this CommentItem e) => new()
    {
        VideoId = e.VideoId,
        ParentId = e.ParentId,
        AuthorId = e.AuthorId,
        AuthorName = e.AuthorName,
        AuthorHandle = e.AuthorHandle,
        Text = e.Text,
        CreatedAtUtc = e.CreatedAtUtc,
        Mentions = [.. e.Mentions.Select(x => new TCommentMention { UserId = x.UserId, Handle = x.Handle })]
    };
}
