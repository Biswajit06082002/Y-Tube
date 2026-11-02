namespace Video.Api.Models;

/// <summary>
/// The multipart form the upload page posts. HTTP-specific (IFormFile), so it lives in the API
/// layer; the controller turns it into a plain UploadVideoCommand for the BLL.
/// </summary>
public sealed class UploadVideoForm
{
    public IFormFile? Video { get; set; }
    public IFormFile? Thumbnail { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }

    /// <summary>Comma-separated, e.g. "angular,signals,tutorial".</summary>
    public string? Tags { get; set; }
}
