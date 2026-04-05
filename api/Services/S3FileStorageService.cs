using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Serilog;

namespace Api.Services;

/// <summary>
/// Production file storage using S3-compatible object storage (AWS S3, Cloudflare R2, etc.).
/// Reads configuration from ISettingsService.
/// </summary>
public class S3FileStorageService(ISettingsService settings) : IFileStorageService
{
    private static readonly HashSet<string> AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"];
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public async Task<string> SaveAsync(Stream fileStream, string entityType, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => throw new InvalidOperationException($"Unsupported file type: {extension}. Allowed: .jpg, .png, .webp")
        };

        if (!AllowedContentTypes.Contains(contentType))
            throw new InvalidOperationException($"Unsupported content type: {contentType}");

        if (fileStream.CanSeek && fileStream.Length > MaxFileSizeBytes)
            throw new InvalidOperationException($"File exceeds maximum size of {MaxFileSizeBytes / 1024 / 1024}MB");

        var key = $"{entityType}/{Guid.NewGuid()}{extension}";
        var client = await GetClientAsync();
        var bucket = await settings.GetAsync("s3_bucket");

        // Buffer into MemoryStream so the SDK knows Content-Length upfront.
        // R2 rejects chunked/streaming uploads (STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER).
        var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms);
        ms.Position = 0;

        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = ms,
            ContentType = contentType,
            DisablePayloadSigning = true,   // R2: use UNSIGNED-PAYLOAD instead of chunk signing
        };

        await client.PutObjectAsync(request);
        Log.Information("[S3] Uploaded {Key} to {Bucket}", key, bucket);
        return key;
    }

    public async Task SaveWithKeyAsync(Stream fileStream, string key, string contentType)
    {
        var client = await GetClientAsync();
        var bucket = await settings.GetAsync("s3_bucket");

        // Buffer into MemoryStream so the SDK knows Content-Length upfront.
        var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms);
        ms.Position = 0;

        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = ms,
            ContentType = contentType,
            DisablePayloadSigning = true,   // R2: use UNSIGNED-PAYLOAD instead of chunk signing
            Headers = { CacheControl = "public, max-age=31536000, immutable" }
        };

        await client.PutObjectAsync(request);
        Log.Information("[S3] Uploaded {Key} to {Bucket}", key, bucket);
    }

    public async Task<bool> DeleteAsync(string path)
    {
        var client = await GetClientAsync();
        var bucket = await settings.GetAsync("s3_bucket");

        try
        {
            await client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = bucket,
                Key = path
            });
            Log.Information("[S3] Deleted {Path} from {Bucket}", path, bucket);
            return true;
        }
        catch (AmazonS3Exception ex)
        {
            Log.Error(ex, "[S3] Failed to delete {Path}", path);
            return false;
        }
    }

    public string GetPublicUrl(string path)
    {
        // CDN base URL is loaded synchronously since this is called in mapping contexts.
        // The setting should be cached by SettingsService (Redis 30s TTL).
        var cdnBaseUrl = settings.GetOrDefaultAsync("cdn_base_url", "").GetAwaiter().GetResult();
        if (string.IsNullOrEmpty(cdnBaseUrl))
            return path;
        return $"{cdnBaseUrl.TrimEnd('/')}/{path}";
    }

    private async Task<AmazonS3Client> GetClientAsync()
    {
        var accessKey = await settings.GetAsync("s3_access_key");
        var secretKey = await settings.GetAsync("s3_secret_key");
        var region = await settings.GetOrDefaultAsync("s3_region", "us-east-1") ?? "us-east-1";
        var endpointUrl = await settings.GetOrDefaultAsync("s3_endpoint_url", "");

        var config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(region),
        };

        if (!string.IsNullOrEmpty(endpointUrl))
        {
            config.ServiceURL = endpointUrl;
            config.ForcePathStyle = true;
        }

        return new AmazonS3Client(accessKey, secretKey, config);
    }
}
