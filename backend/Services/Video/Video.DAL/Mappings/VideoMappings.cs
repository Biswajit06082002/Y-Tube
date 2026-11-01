using Video.DAL.Models;
using Video.Entities;

namespace Video.DAL.Mappings;

internal static class VideoMappings
{
    public static VideoItem ToEntity(this TVideo m) =>
        new(m.Id, m.Title, m.Description, m.Category, m.ChannelId, m.ChannelName,
            m.Views, m.DurationSeconds, m.PublishedAtUtc, m.BaseLikes);
}
