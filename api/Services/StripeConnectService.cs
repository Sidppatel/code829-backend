using Serilog;
using Stripe;

namespace Api.Services;

public class StripeConnectService(ISecretsProvider secrets) : IStripeConnectService
{

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

            BusinessType = prefill.BusinessType,
            BusinessProfile = new AccountBusinessProfileOptions
            {
                Mcc = prefill.Mcc,
                Name = prefill.LegalName,
                ProductDescription = prefill.ProductDescription,
                Url = PlatformMarketingUrl,
                SupportEmail = contactEmail
            },

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

        try
        {
            Log.Information("[StripeConnect] Creating onboarding link for account {AccountId} (scope={Scope})",
                stripeAccountId, scope);
            var link = await service.CreateAsync(options);

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

            var getOptions = new AccountGetOptions
            {
                Expand = new List<string> { "external_accounts" }
            };
            var account = await service.GetAsync(stripeAccountId, getOptions);
            var requirements = account.Requirements?.CurrentlyDue?.ToList() ?? new List<string>();

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
