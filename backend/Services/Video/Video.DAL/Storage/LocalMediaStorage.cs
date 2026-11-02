using Microsoft.Extensions.Options;
using Video.BLL.Interfaces;

namespace Video.DAL.Storage;

public sealed class MediaStorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Media root on disk, outside the repository (e.g. D:\YTubeMedia).</summary>
    public string RootPath { get; init; } = string.Empty;
}

/// <summary>
/// Stores media on the local disk:
///   {root}/uploads/{videoId}/source.mp4      ← originals (never served publicly)
///   {root}/videos/{videoId}/thumbnail.jpg    ← served under /media/videos/…
///   {root}/videos/{videoId}/hls/…            ← written by the Processing worker
/// In production this becomes object storage (S3 / Azure Blob) behind a CDN; only this class changes.
/// </summary>
public sealed class LocalMediaStorage(IOptions<MediaStorageOptions> options) : IMediaStorage
{
    private readonly string _root = options.Value.RootPath;

    public Task<string> SaveSourceAsync(string videoId, Stream content, string extension, CancellationToken ct = default) =>
        SaveAsync($"uploads/{videoId}/source{extension}", content, ct);

    public Task<string> SaveThumbnailAsync(string videoId, Stream content, string extension, CancellationToken ct = default) =>
        SaveAsync($"videos/{videoId}/thumbnail{extension}", content, ct);

    public Task DeleteAllAsync(string videoId, CancellationToken ct = default)
    {
        foreach (var dir in new[] { $"uploads/{videoId}", $"videos/{videoId}" })
        {
            var full = FullPath(dir);
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Streams to a temp file and renames it at the end, so a half-written file (crash, cancelled
    /// upload) never appears under its final name.
    /// </summary>
    private async Task<string> SaveAsync(string relativePath, Stream content, CancellationToken ct)
    {
        var target = FullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        var temp = target + ".partial";
        await using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, useAsync: true))
        {
            await content.CopyToAsync(file, 1 << 20, ct);
        }

        File.Move(temp, target, overwrite: true);
        return relativePath;
    }

    private string FullPath(string relativePath) =>
        Path.GetFullPath(Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
}
