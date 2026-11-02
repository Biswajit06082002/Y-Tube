using Video.DAL.Models;
using Video.Entities;

namespace Video.DAL.Mappings;

internal static class VideoMappings
{
    public static VideoItem ToEntity(this TVideo m) => new()
    {
        Id = m.Id,
        OwnerId = m.OwnerId,
        Title = m.Title,
        Description = m.Description,
        Category = m.Category,
        Tags = [.. m.Tags.Select(t => t.Tag).Order()],
        ChannelId = m.ChannelId,
        ChannelName = m.ChannelName,
        Views = m.Views,
        BaseLikes = m.BaseLikes,
        Status = (VideoStatus)m.Status,
        ProcessingProgress = m.ProcessingProgress,
        ProcessingError = m.ProcessingError,
        DurationSeconds = m.DurationSeconds,
        Width = m.Width,
        Height = m.Height,
        Renditions = [.. m.Renditions.Split(',', StringSplitOptions.RemoveEmptyEntries)],
        SourcePath = m.SourcePath,
        ThumbnailPath = m.ThumbnailPath,
        MasterPlaylistPath = m.MasterPlaylistPath,
        CreatedAtUtc = m.CreatedAtUtc,
        PublishedAtUtc = m.PublishedAtUtc
    };

    public static TVideo ToModel(this VideoItem e) => new()
    {
        Id = e.Id,
        OwnerId = e.OwnerId,
        Title = e.Title,
        Description = e.Description,
        Category = e.Category,
        Tags = [.. e.Tags.Select(t => new TVideoTag { VideoId = e.Id, Tag = t })],
        ChannelId = e.ChannelId,
        ChannelName = e.ChannelName,
        Views = e.Views,
        BaseLikes = e.BaseLikes,
        Status = (int)e.Status,
        ProcessingProgress = e.ProcessingProgress,
        ProcessingError = e.ProcessingError,
        DurationSeconds = e.DurationSeconds,
        Width = e.Width,
        Height = e.Height,
        Renditions = string.Join(',', e.Renditions),
        SourcePath = e.SourcePath,
        ThumbnailPath = e.ThumbnailPath,
        MasterPlaylistPath = e.MasterPlaylistPath,
        CreatedAtUtc = e.CreatedAtUtc,
        PublishedAtUtc = e.PublishedAtUtc
    };
}
