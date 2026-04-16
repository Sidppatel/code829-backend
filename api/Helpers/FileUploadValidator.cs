namespace Api.Helpers;

public static class FileUploadValidator
{
    private static readonly string[] AllowedContentTypes =
        ["image/jpeg", "image/png", "image/webp", "image/gif"];

    private static readonly string[] AllowedExtensions =
        [".jpg", ".jpeg", ".png", ".webp", ".gif"];

    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public static (bool IsValid, string? Error) Validate(IFormFile file)
    {
        if (file.Length == 0)
            return (false, "File is empty");

        if (file.Length > MaxFileSizeBytes)
            return (false, $"File exceeds the {MaxFileSizeBytes / (1024 * 1024)} MB limit");

        if (!AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return (false, $"File type '{file.ContentType}' is not allowed. Accepted types: JPEG, PNG, WebP, GIF");

        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            return (false, $"File extension '{ext}' is not allowed. Accepted: .jpg, .jpeg, .png, .webp, .gif");

        return (true, null);
    }
}
