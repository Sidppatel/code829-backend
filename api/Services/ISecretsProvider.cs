namespace Api.Services;

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

    string FrontendUrlAdmin { get; }

    string GoogleOAuthClientId { get; }
    string GoogleOAuthClientSecret { get; }
}
