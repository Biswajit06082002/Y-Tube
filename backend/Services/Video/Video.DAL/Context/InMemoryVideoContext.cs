using System.Collections.Concurrent;
using Video.DAL.Models;

namespace Video.DAL.Context;

/// <summary>
/// In-memory "database" for videos until uploads are built (Phase 2) — plays the role a DbContext
/// will play then. Data is generated from a fixed seed, so ids stay the same across restarts.
/// Likes live in memory and reset on restart.
/// </summary>
public sealed class InMemoryVideoContext
{
    private static readonly (string Category, string[] Titles)[] Seed =
    [
        ("System Design", ["Designing YouTube from scratch", "Rate limiting explained in 10 minutes", "Consistent hashing, visually", "How Netflix serves video at scale", "CAP theorem without the jargon", "Caching strategies you should know"]),
        ("Coding", ["Angular signals in 15 minutes", ".NET 10: what's new", "Clean architecture in ASP.NET Core", "EF Core performance tips", "TypeScript generics finally make sense", "Build a REST API in one sitting"]),
        ("Music", ["Lo-fi beats to code to", "Acoustic covers, live session", "Piano for deep focus — 1 hour", "Synthwave night drive mix"]),
        ("Gaming", ["Speedrun world record attempt", "Indie games you missed this year", "Building a game engine: day 1", "Retro console restoration"]),
        ("Cooking", ["15-minute weeknight biryani", "Perfect homemade naan", "Street food tour: Kolkata", "Meal prep for a busy week"]),
        ("Travel", ["Sikkim road trip on a budget", "48 hours in Tokyo", "Hidden beaches of Goa"]),
        ("Data & ML", ["NumPy crash course", "Pandas for absolute beginners", "How recommendation systems work", "Linear regression from scratch"])
    ];

    private static readonly (string Id, string Name)[] Channels =
    [
        ("ch_bytebuilder", "ByteBuilder"), ("ch_scalewise", "ScaleWise"), ("ch_dotnetdaily", "DotNet Daily"),
        ("ch_ngcraft", "NgCraft"), ("ch_chillwave", "Chillwave Studio"), ("ch_pixelquest", "PixelQuest"),
        ("ch_masalalab", "Masala Lab"), ("ch_roamfree", "Roam Free"), ("ch_datadesk", "Data Desk")
    ];

    public InMemoryVideoContext()
    {
        var random = new Random(42);
        var now = DateTime.UtcNow;
        var videos = new List<TVideo>();

        foreach (var (category, titles) in Seed)
        {
            foreach (var title in titles)
            {
                var channel = Channels[random.Next(Channels.Length)];
                var views = (long)Math.Pow(10, 2 + random.NextDouble() * 5);
                videos.Add(new TVideo
                {
                    Id = NewId(random),
                    Title = title,
                    Description = $"{title}. A sample video in {category} from {channel.Name}. " +
                                  "Real uploads and descriptions arrive with the Video service in Phase 2.",
                    Category = category,
                    ChannelId = channel.Id,
                    ChannelName = channel.Name,
                    Views = views,
                    DurationSeconds = random.Next(45, 3600),
                    PublishedAtUtc = now.AddHours(-random.Next(2, 24 * 400)),
                    BaseLikes = (int)(views * (0.01 + random.NextDouble() * 0.04))
                });
            }
        }

        // Shuffle once so the feed mixes categories.
        Videos = [.. videos.OrderBy(_ => random.Next())];
        VideosById = Videos.ToDictionary(v => v.Id);
        Categories = [.. Seed.Select(s => s.Category)];
    }

    /// <summary>"Videos table".</summary>
    public IReadOnlyList<TVideo> Videos { get; }
    public IReadOnlyDictionary<string, TVideo> VideosById { get; }
    public IReadOnlyList<string> Categories { get; }

    /// <summary>"VideoLikes table": videoId → set of userIds. Concurrent because requests run in parallel.</summary>
    public ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> Likes { get; } = new();

    private static string NewId(Random random)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
        return new string(Enumerable.Range(0, 11).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }
}
