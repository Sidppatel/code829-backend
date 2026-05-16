using System.Security.Cryptography;
using System.Text;

namespace IntegrationTests.Fixtures;

public static class StripeWebhookSigner
{
    public const string SecretEnvVar = "STRIPE_WEBHOOK_SECRET";

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

    public static string LoadFixture(string fileName, string stripeAccountId, Guid? organizationId = null)
    {

        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "StripeWebhookFixtures", fileName);
        var raw = File.ReadAllText(path);
        return raw
            .Replace("{{STRIPE_ACCOUNT_ID}}", stripeAccountId)
            .Replace("{{ORGANIZATION_ID}}", (organizationId ?? Guid.Empty).ToString());
    }
}
