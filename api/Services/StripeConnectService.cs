using Serilog;
using Stripe;

namespace Api.Services;

/// <summary>
/// Production implementation of <see cref="IStripeConnectService"/> using the
/// Stripe.net SDK against the live Connect API.
///
/// <para>
/// All Stripe interactions are logged at Info level with redacted IDs (account
/// id is included in full because it is the join key on our side; no PII is
/// emitted). Failures from the SDK are mapped to native exceptions so callers
/// don't need to take a Stripe.net dependency.
/// </para>
///
/// <para>
/// Registered as a singleton because <see cref="ISecretsProvider"/> is a
/// singleton — the secret key is read on every call rather than cached, so
/// rotation via env-var change + restart picks up cleanly.
/// </para>
/// </summary>
public class StripeConnectService(ISecretsProvider secrets) : IStripeConnectService
{
    public async Task<string> CreateExpressAccountAsync(Guid organizationId, string contactEmail, string countryCode)
    {
        var client = GetClient();
        var service = new AccountService(client);

        var options = new AccountCreateOptions
        {
            Type = "express",
            Country = countryCode,
            Email = contactEmail,
            Capabilities = new AccountCapabilitiesOptions
            {
                CardPayments = new AccountCapabilitiesCardPaymentsOptions { Requested = true },
                Transfers = new AccountCapabilitiesTransfersOptions { Requested = true }
            },
            // organization_id is the canonical join key on our side; persisting
            // it on the Stripe account makes dashboard / webhook payloads
            // reconcilable without a DB lookup.
            Metadata = new Dictionary<string, string>
            {
                ["organization_id"] = organizationId.ToString()
            }
        };

        try
        {
            Log.Information("[StripeConnect] Creating Express account for organization {OrganizationId} (country={Country})",
                organizationId, countryCode);
            var account = await service.CreateAsync(options);
            Log.Information("[StripeConnect] Created account {AccountId} for organization {OrganizationId}",
                account.Id, organizationId);
            return account.Id;
        }
        catch (StripeException ex)
        {
            Log.Error(ex, "[StripeConnect] Failed to create Express account for organization {OrganizationId}", organizationId);
            throw MapStripeException(ex);
        }
    }

    public async Task<string> CreateOnboardingLinkAsync(string stripeAccountId, OnboardingLinkScope scope)
    {
        var client = GetClient();
        var service = new AccountLinkService(client);

        // Both URLs land on the admin app. The settings page handles ?status=
        // to decide whether to refresh status or surface an error.
        var adminBase = secrets.FrontendUrlAdmin.TrimEnd('/');
        var returnUrl = $"{adminBase}/settings/stripe/return?status=complete";
        var refreshUrl = $"{adminBase}/settings/stripe/refresh";

        var options = new AccountLinkCreateOptions
        {
            Account = stripeAccountId,
            Type = "account_onboarding",
            ReturnUrl = returnUrl,
            RefreshUrl = refreshUrl,
            CollectionOptions = new AccountLinkCollectionOptionsOptions
            {
                Fields = "currently_due"
            }
        };

        if (scope == OnboardingLinkScope.BankOnly)
        {
            // Restricts collection to just external_account (bank). The
            // Stripe API supports collection_options[requirements][only][]
            // but Stripe.net 51 doesn't surface it as a typed property, so
            // we pass it via ExtraParams using the dotted-key form the SDK
            // serializes to form-encoded body params.
            options.ExtraParams = new Dictionary<string, object>
            {
                ["collection_options[requirements][only][]"] = "external_account"
            };
        }

        try
        {
            Log.Information("[StripeConnect] Creating onboarding link for account {AccountId} (scope={Scope})",
                stripeAccountId, scope);
            var link = await service.CreateAsync(options);
            // Stripe.net surfaces ExpiresAt as a DateTime already; format ISO-8601 for logs.
            Log.Information("[StripeConnect] Created onboarding link for account {AccountId} expiring at {ExpiresAt:o}",
                stripeAccountId, link.ExpiresAt);
            return link.Url;
        }
        catch (StripeException ex)
        {
            Log.Error(ex, "[StripeConnect] Failed to create onboarding link for account {AccountId}", stripeAccountId);
            throw MapStripeException(ex);
        }
    }

    public async Task<StripeAccountStatus> FetchAccountStatusAsync(string stripeAccountId)
    {
        var client = GetClient();
        var service = new AccountService(client);

        try
        {
            Log.Information("[StripeConnect] Fetching account status for {AccountId}", stripeAccountId);
            var account = await service.GetAsync(stripeAccountId);
            var requirements = account.Requirements?.CurrentlyDue?.ToList() ?? new List<string>();

            return new StripeAccountStatus(
                AccountId: account.Id,
                ChargesEnabled: account.ChargesEnabled,
                PayoutsEnabled: account.PayoutsEnabled,
                DetailsSubmitted: account.DetailsSubmitted,
                RequirementsCurrentlyDue: requirements);
        }
        catch (StripeException ex)
        {
            Log.Error(ex, "[StripeConnect] Failed to fetch account status for {AccountId}", stripeAccountId);
            throw MapStripeException(ex);
        }
    }

    private StripeClient GetClient()
    {
        var key = secrets.StripeSecretKey;
        if (string.IsNullOrEmpty(key))
            throw new InvalidOperationException(
                "Stripe is not configured — set STRIPE_SECRET_KEY environment variable");

        return new StripeClient(key);
    }

    /// <summary>
    /// Translates Stripe SDK exceptions to runtime types callers can pattern-match
    /// against without referencing Stripe.net.
    /// </summary>
    private static Exception MapStripeException(StripeException ex)
    {
        return ex.StripeError?.Type switch
        {
            "invalid_request_error" => new ArgumentException(
                $"Invalid Stripe Connect request: {ex.StripeError.Message}", ex),
            "rate_limit_error" => new InvalidOperationException(
                "Stripe API rate limit hit — try again shortly", ex),
            _ => new InvalidOperationException(
                $"Stripe Connect error: {ex.Message}", ex)
        };
    }
}
