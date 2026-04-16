namespace Api.Services;

/// <summary>
/// Reads application secrets from IConfiguration (environment variables / .env).
/// Registered as singleton — env vars do not change at runtime.
/// </summary>
public class SecretsProvider(IConfiguration configuration) : ISecretsProvider
{
    public string JwtSecret => configuration["JWT_SECRET"]
        ?? throw new InvalidOperationException("JWT_SECRET environment variable is required");

    public string StripeSecretKey => configuration["STRIPE_SECRET_KEY"] ?? "";
    public string StripePublishableKey => configuration["STRIPE_PUBLISHABLE_KEY"] ?? "";
    public string StripeWebhookSecret => configuration["STRIPE_WEBHOOK_SECRET"] ?? "";
    public string ResendApiKey => configuration["RESEND_API_KEY"] ?? "";
    public string S3AccessKey => configuration["S3_ACCESS_KEY"] ?? "";
    public string S3SecretKey => configuration["S3_SECRET_KEY"] ?? "";
    public string S3Bucket => configuration["S3_BUCKET"] ?? "";
    public string S3EndpointUrl => configuration["S3_ENDPOINT_URL"] ?? "";
    public string CdnBaseUrl => configuration["CDN_BASE_URL"] ?? "";
}
