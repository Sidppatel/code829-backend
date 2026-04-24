namespace Api.Services;

/// <summary>
/// Provides strongly-typed access to application secrets from environment variables.
/// Secrets are never stored in the database — only in env vars / .env files.
/// </summary>
public interface ISecretsProvider
{
    string JwtSecret { get; }
    string? JwtSecretPrevious { get; }
    string StripeSecretKey { get; }
    string StripePublishableKey { get; }
    string StripeWebhookSecret { get; }
    string ResendApiKey { get; }
    string S3AccessKey { get; }
    string S3SecretKey { get; }
    string S3Bucket { get; }
    string S3EndpointUrl { get; }
    string CdnBaseUrl { get; }
}
