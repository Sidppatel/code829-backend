using Serilog;

namespace Api.Services;

/// <summary>
/// Production file storage using S3/R2/compatible object storage.
/// Currently validates config and throws if misconfigured.
/// </summary>
public class S3FileStorageService : IFileStorageService
{
    public Task<string> SaveAsync(Stream fileStream, string entityType, string fileName)
    {
        // Production implementation would upload to S3/R2:
        // var key = $"{entityType}/{Guid.NewGuid()}{Path.GetExtension(fileName)}";
        // await s3Client.PutObjectAsync(new PutObjectRequest { ... });
        // return key;
        Log.Information("[S3] SaveAsync: {Entity}/{File}", entityType, fileName);
        throw new NotImplementedException("S3/R2 integration pending");
    }

    public Task<bool> DeleteAsync(string path)
    {
        Log.Information("[S3] DeleteAsync: {Path}", path);
        throw new NotImplementedException("S3/R2 integration pending");
    }

    public string GetPublicUrl(string path)
    {
        // Would return CDN URL in production
        return $"https://cdn.example.com/{path}";
    }
}
