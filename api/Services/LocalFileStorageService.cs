using Api.Exceptions;
using Serilog;

namespace Api.Services;

public class LocalFileStorageService(IMalwareScanner scanner) : IFileStorageService
{
    private const string UploadsDir = "uploads";
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    private static readonly Dictionary<string, string[]> ExtensionToContentTypes = new()
    {
        [".jpg"] = ["image/jpeg"],
        [".jpeg"] = ["image/jpeg"],
        [".png"] = ["image/png"],
        [".gif"] = ["image/gif"],
        [".webp"] = ["image/webp"],
    };

    public async Task<string> SaveAsync(Stream fileStream, string entityType, string fileName)
    {

        fileName = Path.GetFileName(fileName);

        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        if (!AllowedExtensions.Contains(ext))
            throw new InvalidOperationException($"File extension '{ext}' is not allowed. Allowed: {string.Join(", ", AllowedExtensions)}");

        if (fileStream.CanSeek && fileStream.Length > MaxFileSizeBytes)
            throw new InvalidOperationException($"File exceeds maximum allowed size of {MaxFileSizeBytes / (1024 * 1024)} MB");

        var storedName = $"{Guid.NewGuid():N}{ext}";
        var relativePath = Path.Combine(entityType, storedName);
        var fullPath = Path.Combine(UploadsDir, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms);
        ms.Position = 0;
        var scan = await scanner.ScanAsync(ms);
        if (!scan.IsClean)
            throw new MalwareDetectedException(scan.Threat);
        ms.Position = 0;

        await using var fs = File.Create(fullPath);
        await ms.CopyToAsync(fs);

        Log.Information("[LocalStorage] Saved {Path}", relativePath);
        return relativePath;
    }

    public async Task SaveWithKeyAsync(Stream fileStream, string key, string contentType)
    {
        var fullPath = Path.Combine(UploadsDir, key);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms);
        ms.Position = 0;
        var scan = await scanner.ScanAsync(ms);
        if (!scan.IsClean)
            throw new MalwareDetectedException(scan.Threat);
        ms.Position = 0;

        await using var fs = File.Create(fullPath);
        await ms.CopyToAsync(fs);

        Log.Information("[LocalStorage] Saved {Key}", key);
    }

    public Task<bool> DeleteAsync(string path)
    {
        var fullPath = Path.Combine(UploadsDir, path);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public string GetPublicUrl(string path)
    {
        return $"/uploads/{path.Replace('\\', '/')}";
    }
}
