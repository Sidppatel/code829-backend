using SkiaSharp;

namespace Api.Helpers;

public static class FileUploadValidator
{
    private static readonly string[] AllowedContentTypes =
        ["image/jpeg", "image/png", "image/webp", "image/gif"];

    private static readonly string[] AllowedExtensions =
        [".jpg", ".jpeg", ".png", ".webp", ".gif"];

    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

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

        if (!HasAllowedImageSignature(file))
            return (false, "File content does not match an allowed image format");

        if (!TryDecode(file))
            return (false, "File could not be decoded as a valid image");

        return (true, null);
    }

    private static bool TryDecode(IFormFile file)
    {
        try
        {
            using var stream = file.OpenReadStream();
            using var skData = SKData.Create(stream);
            using var codec = SKCodec.Create(skData);
            return codec != null && codec.Info.Width > 0 && codec.Info.Height > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasAllowedImageSignature(IFormFile file)
    {
        Span<byte> head = stackalloc byte[12];
        using var stream = file.OpenReadStream();
        var read = 0;
        while (read < head.Length)
        {
            var n = stream.Read(head[read..]);
            if (n == 0) break;
            read += n;
        }
        if (read < 3) return false;

        if (read >= 8 &&
            head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47 &&
            head[4] == 0x0D && head[5] == 0x0A && head[6] == 0x1A && head[7] == 0x0A)
            return true;

        if (head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
            return true;

        if (read >= 6 &&
            head[0] == 0x47 && head[1] == 0x49 && head[2] == 0x46 && head[3] == 0x38 &&
            (head[4] == 0x37 || head[4] == 0x39) && head[5] == 0x61)
            return true;

        if (read >= 12 &&
            head[0] == 0x52 && head[1] == 0x49 && head[2] == 0x46 && head[3] == 0x46 &&
            head[8] == 0x57 && head[9] == 0x45 && head[10] == 0x42 && head[11] == 0x50)
            return true;

        return false;
    }
}
