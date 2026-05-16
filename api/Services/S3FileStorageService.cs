using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Api.Exceptions;
using Api.Helpers;
using Serilog;

namespace Api.Services;

public class S3FileStorageService(ISecretsProvider secrets, IMalwareScanner scanner) : IFileStorageService
{
    private static readonly HashSet<string> AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"];
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private AmazonS3Client? _client;
    private readonly object _clientLock = new();

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

        var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms);
        ms.Position = 0;

        var scan = await scanner.ScanAsync(ms);
        if (!scan.IsClean)
            throw new MalwareDetectedException(scan.Threat);
        ms.Position = 0;

        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = ms,
            ContentType = contentType,
            DisablePayloadSigning = true,

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
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var client = GetClient();
        var bucket = secrets.S3Bucket;

        Stream uploadStream;
        bool ownedStream = false;
        if (fileStream is MemoryStream existing && existing.CanSeek)
        {
            existing.Position = 0;
            uploadStream = existing;
        }
        else
        {
            var ms = new MemoryStream();
            await fileStream.CopyToAsync(ms);
            ms.Position = 0;
            uploadStream = ms;
            ownedStream = true;
        }

        var scanStart = sw.ElapsedMilliseconds;
        var scan = await scanner.ScanAsync(uploadStream);
        if (!scan.IsClean)
            throw new MalwareDetectedException(scan.Threat);
        uploadStream.Position = 0;
        var afterScan = sw.ElapsedMilliseconds;

        var bytes = uploadStream.Length;

        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = uploadStream,
            ContentType = contentType,
            DisablePayloadSigning = true,
            AutoCloseStream = ownedStream,
            Headers =
            {
                CacheControl = "public, max-age=31536000, immutable",
                ContentDisposition = "attachment"
            }
        };

        await RetryHelper.WithRetryAsync(
            () => client.PutObjectAsync(request),
            context: "S3 upload");
        var afterPut = sw.ElapsedMilliseconds;

        Log.Information("[S3] Uploaded {Key} ({Bytes}b) bucket={Bucket} timing prep={Prep}ms scan={Scan}ms put={Put}ms total={Total}ms",
            key, bytes, bucket, scanStart, afterScan - scanStart, afterPut - afterScan, sw.ElapsedMilliseconds);
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
        if (_client is not null) return _client;
        lock (_clientLock)
        {
            if (_client is not null) return _client;

            var accessKey = secrets.S3AccessKey;
            var secretKey = secrets.S3SecretKey;

            var region = Environment.GetEnvironmentVariable("S3_REGION") ?? "us-east-1";
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

            _client = new AmazonS3Client(accessKey, secretKey, config);
            return _client;
        }
    }
}
