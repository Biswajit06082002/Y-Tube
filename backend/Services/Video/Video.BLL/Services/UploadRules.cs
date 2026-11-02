namespace Video.BLL.Services;

/// <summary>Business limits for uploads, in one place so the API layer and the BLL agree.</summary>
public static class UploadRules
{
    public const long MaxVideoBytes = 2L * 1024 * 1024 * 1024; // 2 GB
    public const long MaxThumbnailBytes = 5L * 1024 * 1024;    // 5 MB
    public const int MaxTitleLength = 100;
    public const int MaxDescriptionLength = 5000;
    public const int MaxTags = 15;
    public const int MaxTagLength = 30;

    public static readonly string[] VideoExtensions = [".mp4", ".mov", ".m4v", ".mkv", ".webm", ".avi"];
    public static readonly string[] ThumbnailExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    public static readonly string[] Categories =
        ["System Design", "Coding", "Data & ML", "Music", "Gaming", "Cooking", "Travel", "Education", "Other"];
}
