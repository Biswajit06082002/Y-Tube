using Microsoft.Extensions.Options;
using Processing.BLL.Interfaces;
using Processing.Entities;

namespace Processing.DAL.Storage;

public sealed class MediaStorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Same media root the Video service uses (e.g. D:\YTubeMedia).</summary>
    public string RootPath { get; init; } = string.Empty;
}

/// <summary>
/// The worker's view of the shared media folder. Locally both services see the same disk; in
/// production the worker would download the original from object storage and upload the output.
/// </summary>
public sealed class LocalMediaFiles(IOptions<MediaStorageOptions> options) : IMediaFiles
{
    private readonly string _root = options.Value.RootPath;

    public string AbsolutePath(string relativePath) =>
        Path.GetFullPath(Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    public HlsOutput PrepareHlsDirectory(string videoId)
    {
        var relative = $"videos/{videoId}/hls";
        var absolute = AbsolutePath(relative);

        // Idempotent: a redelivered job starts from a clean folder instead of mixing old and new segments.
        if (Directory.Exists(absolute)) Directory.Delete(absolute, recursive: true);
        Directory.CreateDirectory(absolute);
        return new HlsOutput(absolute, relative);
    }

    public Task WriteTextAsync(string absolutePath, string content, CancellationToken ct = default) =>
        File.WriteAllTextAsync(absolutePath, content, ct);

    public void DeleteHlsDirectory(string videoId)
    {
        var absolute = AbsolutePath($"videos/{videoId}/hls");
        if (Directory.Exists(absolute)) Directory.Delete(absolute, recursive: true);
    }
}
