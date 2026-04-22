using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Api.Helpers;
using Serilog;

namespace Api.Services;

/// <summary>
/// Production file storage using S3-compatible object storage (AWS S3, Cloudflare R2, etc.).
/// Reads configuration from ISettingsService.
/// </summary>
public class S3FileStorageService(ISecretsProvider secrets, ISettingsService settings) : IFileStorageService
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
        var client = GetClient();
        var bucket = secrets.S3Bucket;

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
            // Force download disposition so a malicious polyglot can never execute inline.
            Headers = { ContentDisposition = "attachment" }
        };

        await RetryHelper.WithRetryAsync(
            () => client.PutObjectAsync(request),
            context: "S3 upload");
        Log.Information("[S3] Uploaded {Key} to {Bucket}", key, bucket);
        return key;
    }

    public async Task SaveWithKeyAsync(Stream fileStream, string key, string contentType)
    {
        var client = GetClient();
        var bucket = secrets.S3Bucket;

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
            Headers =
            {
                CacheControl = "public, max-age=31536000, immutable",
                ContentDisposition = "attachment"
            }
        };

        await RetryHelper.WithRetryAsync(
            () => client.PutObjectAsync(request),
            context: "S3 upload");
        Log.Information("[S3] Uploaded {Key} to {Bucket}", key, bucket);
    }

    public async Task<bool> DeleteAsync(string path)
    {
        var client = GetClient();
        var bucket = secrets.S3Bucket;

        try
        {
            await RetryHelper.WithRetryAsync(
                () => client.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = bucket,
                    Key = path
                }),
                context: "S3 delete");
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
        var cdnBaseUrl = secrets.CdnBaseUrl;
        if (string.IsNullOrEmpty(cdnBaseUrl))
            return path;
        return $"{cdnBaseUrl.TrimEnd('/')}/{path}";
    }

    private AmazonS3Client GetClient()
    {
        var accessKey = secrets.S3AccessKey;
        var secretKey = secrets.S3SecretKey;
        var region = settings.GetOrDefaultAsync("s3_region", "us-east-1").GetAwaiter().GetResult() ?? "us-east-1";
        var endpointUrl = secrets.S3EndpointUrl;

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
