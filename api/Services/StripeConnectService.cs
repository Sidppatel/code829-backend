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
    // Public marketing URL passed to Stripe as business_profile.url. Stripe
    // rejects localhost / private hostnames here, so this MUST be a public,
    // TLS-bearing TLD even in dev — Stripe never actually fetches it during
    // onboarding, but the syntactic check is enforced. The exact origin doesn't
    // need to match the FrontendUrlAdmin setting (which is local in dev).
    private const string PlatformMarketingUrl = "https://code829.com";

    public async Task<string> CreateExpressAccountAsync(Guid organizationId, string contactEmail, string countryCode, StripeBusinessProfilePrefill prefill)
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
            // Prefilling business_type + business_profile collapses Stripe's
            // "Tell us about your business" onboarding screen. To skip the
            // screen entirely Stripe requires ALL of: mcc, name, url,
            // product_description (per Stripe Connect Express docs). Omitting
            // url forces the screen back even when product_description is set.
            BusinessType = prefill.BusinessType,
            BusinessProfile = new AccountBusinessProfileOptions
            {
                Mcc = prefill.Mcc,
                Name = prefill.LegalName,
                ProductDescription = prefill.ProductDescription,
                Url = PlatformMarketingUrl,
                SupportEmail = contactEmail
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

        // NOTE: `collection_options.requirements.only` (e.g. ["external_account"])
        // is only supported by the Account Sessions / embedded-onboarding flow —
        // hosted AccountLinks reject it with "Received unknown parameter:
        // collection_options[requirements]". When `scope == BankOnly` we instead
        // rely on `fields = currently_due`: once identity is submitted the only
        // outstanding requirement is the external_account, so Stripe's hosted
        // onboarding naturally collects bank-only without an explicit filter.
        // To force bank-only before identity completion we'd have to migrate to
        // embedded onboarding components — tracked as future work.

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
            // Expand external_accounts so we can read the bank-account last4
            // off the same call. Stripe omits the external_accounts collection
            // from the default Account payload.
            var getOptions = new AccountGetOptions
            {
                Expand = new List<string> { "external_accounts" }
            };
            var account = await service.GetAsync(stripeAccountId, getOptions);
            var requirements = account.Requirements?.CurrentlyDue?.ToList() ?? new List<string>();

            // Pick the first bank-account-typed external account and format
            // last4 as "**** 4242" for display. IExternalAccount is a union of
            // BankAccount + Card; cards get filtered out.
            string? bankAccountLast4 = null;
            if (account.ExternalAccounts?.Data is { Count: > 0 } externals)
            {
                foreach (var ext in externals)
                {
                    if (ext is BankAccount bank && !string.IsNullOrEmpty(bank.Last4))
                    {
                        bankAccountLast4 = $"**** {bank.Last4}";
                        break;
                    }
                }
            }

            var disabledReason = account.Requirements?.DisabledReason;

            return new StripeAccountStatus(
                AccountId: account.Id,
                ChargesEnabled: account.ChargesEnabled,
                PayoutsEnabled: account.PayoutsEnabled,
                DetailsSubmitted: account.DetailsSubmitted,
                RequirementsCurrentlyDue: requirements,
                BankAccountLast4: bankAccountLast4,
                DisabledReason: disabledReason);
        }
        catch (StripeException ex)
        {
            Log.Error(ex, "[StripeConnect] Failed to fetch account status for {AccountId}", stripeAccountId);
            throw MapStripeException(ex);
        }
    }

    public async Task<string?> CreateLoginLinkAsync(string stripeAccountId)
    {
        var client = GetClient();
        var service = new AccountLoginLinkService(client);

        try
        {
            Log.Information("[StripeConnect] Creating login link for account {AccountId}", stripeAccountId);
            var link = await service.CreateAsync(stripeAccountId);
            Log.Information("[StripeConnect] Created login link for account {AccountId}", stripeAccountId);
            return link.Url;
        }
        catch (StripeException ex)
        {
            // Stripe rejects LoginLink with code "login_link_account_invalid"
            // when the account hasn't completed onboarding. That's an expected
            // pre-onboarding state — surface as null rather than throwing so
            // the FE can simply hide the dashboard button.
            var code = ex.StripeError?.Code;
            var message = ex.StripeError?.Message ?? ex.Message ?? string.Empty;
            if (string.Equals(code, "login_link_account_invalid", StringComparison.OrdinalIgnoreCase)
                || message.Contains("must complete onboarding", StringComparison.OrdinalIgnoreCase))
            {
                Log.Information("[StripeConnect] Skipping login link for account {AccountId} (onboarding incomplete)", stripeAccountId);
                return null;
            }

            Log.Error(ex, "[StripeConnect] Failed to create login link for account {AccountId}", stripeAccountId);
            throw MapStripeException(ex);
        }
    }

    public async Task<bool> DeleteAccountAsync(string stripeAccountId)
    {
        var client = GetClient();
        var service = new AccountService(client);

        try
        {
            Log.Information("[StripeConnect] Deleting account {AccountId}", stripeAccountId);
            var deleted = await service.DeleteAsync(stripeAccountId);
            Log.Information("[StripeConnect] Deleted account {AccountId} (deleted={Deleted})", stripeAccountId, deleted.Deleted);
            return deleted.Deleted ?? true;
        }
        catch (StripeException ex)
        {
            // Stripe returns "account_invalid" with 403 when the account is
            // already gone or owned by a different platform key. Treat as
            // success — the desired post-state (no acct on file) is achieved.
            if (string.Equals(ex.StripeError?.Code, "account_invalid", StringComparison.OrdinalIgnoreCase))
            {
                Log.Information("[StripeConnect] Account {AccountId} not present at Stripe — treating as already-deleted", stripeAccountId);
                return true;
            }
            Log.Error(ex, "[StripeConnect] Failed to delete account {AccountId}", stripeAccountId);
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
