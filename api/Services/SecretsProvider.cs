using System.Text;

namespace Api.Services;

/// <summary>
/// Reads application secrets from IConfiguration (environment variables / .env).
/// Registered as singleton — env vars do not change at runtime.
/// </summary>
public class SecretsProvider(IConfiguration configuration) : ISecretsProvider
{
    public string JwtSecret
    {
        get
        {
            var value = configuration["JWT_SECRET"]
                ?? throw new InvalidOperationException("JWT_SECRET environment variable is required");
            if (Encoding.UTF8.GetBytes(value).Length < 32)
                throw new InvalidOperationException("JWT_SECRET must be at least 32 bytes");
            return value;
        }
    }

    /// <summary>
    /// Optional previous JWT secret kept for rotation grace window.
    /// When set, tokens signed with the previous key still validate; new tokens always sign with JwtSecret.
    /// Must meet the same 32-byte minimum length as JwtSecret.
    /// </summary>
    public string? JwtSecretPrevious
    {
        get
        {
            var value = configuration["JWT_SECRET_PREVIOUS"];
            if (string.IsNullOrEmpty(value)) return null;
            if (Encoding.UTF8.GetBytes(value).Length < 32)
                throw new InvalidOperationException("JWT_SECRET_PREVIOUS must be at least 32 bytes when set");
            return value;
        }
    }

    public string StripeSecretKey => configuration["STRIPE_SECRET_KEY"] ?? "";
    public string StripePublishableKey => configuration["STRIPE_PUBLISHABLE_KEY"] ?? "";
    public string StripeWebhookSecret => configuration["STRIPE_WEBHOOK_SECRET"] ?? "";
    public string ResendApiKey => configuration["RESEND_API_KEY"] ?? "";
    public string S3AccessKey => configuration["S3_ACCESS_KEY"] ?? "";
    public string S3SecretKey => configuration["S3_SECRET_KEY"] ?? "";
    public string S3Bucket => configuration["S3_BUCKET"] ?? "";
    public string S3EndpointUrl => configuration["S3_ENDPOINT_URL"] ?? "";
    public string CdnBaseUrl => configuration["CDN_BASE_URL"] ?? "";

    /// <summary>
    /// Falls back to <c>http://localhost:5174</c> for local development so the
    /// Stripe Connect flow works out of the box without an extra env var.
    /// In Production this should be set explicitly to the deployed admin host.
    /// </summary>
    public string FrontendUrlAdmin =>
        configuration["FRONTEND_URL_ADMIN"] ?? "http://localhost:5174";
}
