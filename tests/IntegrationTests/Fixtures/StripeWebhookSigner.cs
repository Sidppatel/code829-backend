using System.Security.Cryptography;
using System.Text;

namespace IntegrationTests.Fixtures;

/// <summary>
/// Mints a valid <c>Stripe-Signature</c> header for a known payload + webhook
/// secret. Mirrors the format Stripe.net's <c>EventUtility.ConstructEvent</c>
/// expects: <c>t={timestamp},v1={hmac_sha256_hex}</c> where the signed
/// preimage is <c>{timestamp}.{payload}</c>.
///
/// <para>
/// We deliberately keep this small — full Stripe signature semantics
/// (multiple v1= entries, replay-window enforcement) live in the SDK. The
/// only piece our tests need is "make a header that the SDK will accept".
/// </para>
/// </summary>
public static class StripeWebhookSigner
{
    public const string SecretEnvVar = "STRIPE_WEBHOOK_SECRET";

    /// <summary>
    /// Returns a fully formed <c>Stripe-Signature</c> header value for the
    /// given JSON payload, signed with the env-var-configured webhook secret.
    /// </summary>
    public static string Sign(string payload, DateTimeOffset? when = null)
    {
        var secret = Environment.GetEnvironmentVariable(SecretEnvVar)
            ?? throw new InvalidOperationException(
                $"{SecretEnvVar} env var not set — DatabaseFixture should configure it");

        var ts = (when ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds().ToString();
        var preimage = $"{ts}.{payload}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(preimage));
        var sig = Convert.ToHexString(hash).ToLowerInvariant();

        return $"t={ts},v1={sig}";
    }

    /// <summary>
    /// Reads a webhook fixture file from
    /// <c>tests/IntegrationTests/Fixtures/StripeWebhookFixtures/</c> and
    /// substitutes <c>{{STRIPE_ACCOUNT_ID}}</c> + <c>{{ORGANIZATION_ID}}</c>
    /// placeholders before returning the JSON.
    /// </summary>
    public static string LoadFixture(string fileName, string stripeAccountId, Guid? organizationId = null)
    {
        // Tests are run from bin/.../{TFM}; fixtures are copied with PreserveNewest.
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "StripeWebhookFixtures", fileName);
        var raw = File.ReadAllText(path);
        return raw
            .Replace("{{STRIPE_ACCOUNT_ID}}", stripeAccountId)
            .Replace("{{ORGANIZATION_ID}}", (organizationId ?? Guid.Empty).ToString());
    }
}
