namespace Video.Entities.Contracts;

/// <summary>A file part of an upload, independent of ASP.NET's IFormFile so the BLL stays web-agnostic.</summary>
public sealed record UploadedFile(Stream Content, string FileName, long Length);

public sealed record UploadVideoCommand(
    Guid OwnerId,
    string OwnerName,
    string Title,
    string? Description,
    string Category,
    IReadOnlyList<string> Tags,
    UploadedFile? Video,
    UploadedFile? Thumbnail);
