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

    /// <summary>
    /// Origin of the admin SPA (e.g., http://localhost:5174 in dev).
    /// Used by Stripe Connect onboarding to build return/refresh URLs that land
    /// admins back on the settings page after they finish (or bail from)
    /// Stripe-hosted onboarding. Sourced from FRONTEND_URL_ADMIN; not a secret,
    /// but lives here because the same env-var pipeline supplies it.
    /// </summary>
    string FrontendUrlAdmin { get; }
}
