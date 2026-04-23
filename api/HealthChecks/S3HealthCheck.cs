using Amazon.S3;
using Amazon.S3.Model;
using Api.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.HealthChecks;

public class S3HealthCheck(ISecretsProvider secrets) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var accessKey = secrets.S3AccessKey;
            var secretKey = secrets.S3SecretKey;
            var bucket = secrets.S3Bucket;
            var endpointUrl = secrets.S3EndpointUrl;

            if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(bucket))
                return HealthCheckResult.Degraded("S3 credentials not configured");

            var config = new AmazonS3Config { ForcePathStyle = true };
            if (!string.IsNullOrEmpty(endpointUrl))
                config.ServiceURL = endpointUrl;

            using var client = new AmazonS3Client(accessKey, secretKey, config);
            await client.GetBucketLocationAsync(
                new GetBucketLocationRequest { BucketName = bucket },
                cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(exception: ex);
        }
    }
}
