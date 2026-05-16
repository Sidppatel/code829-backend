namespace Api.Services;

public enum OnboardingLinkScope
{
    Identity,
    BankOnly
}

public record StripeAccountStatus(
    string AccountId,
    bool ChargesEnabled,
    bool PayoutsEnabled,
    bool DetailsSubmitted,
    List<string> RequirementsCurrentlyDue,
    string? BankAccountLast4,
    string? DisabledReason);

public record StripeBusinessProfilePrefill(
    string LegalName,
    string ProductDescription,
    string Mcc,
    string BusinessType);

public interface IStripeConnectService
{
    Task<string> CreateExpressAccountAsync(Guid organizationId, string contactEmail, string countryCode, StripeBusinessProfilePrefill prefill);

    Task<string> CreateOnboardingLinkAsync(string stripeAccountId, OnboardingLinkScope scope);

    Task<StripeAccountStatus> FetchAccountStatusAsync(string stripeAccountId);

    Task<string?> CreateLoginLinkAsync(string stripeAccountId);

    Task<bool> DeleteAccountAsync(string stripeAccountId);
}
