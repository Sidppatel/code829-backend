namespace Api.Services;

/// <summary>
/// Scope of an onboarding link generated via <see cref="IStripeConnectService.CreateOnboardingLinkAsync"/>.
///
/// <para>
/// <b>Identity</b>: first-pass onboarding link. Captures KYC details (legal name,
/// SSN, address, etc.) but skips bank-account collection. Relies on the Stripe
/// Connect dashboard toggle "Require at least one bank account?" being set to No.
/// </para>
/// <para>
/// <b>BankOnly</b>: second-pass link. Restricts the requirements to
/// <c>external_account</c> only — i.e., just the bank-account / Financial
/// Connections step. Used after identity is verified to add or update payout
/// destinations.
/// </para>
/// </summary>
public enum OnboardingLinkScope
{
    Identity,
    BankOnly
}

/// <summary>
/// Snapshot of a Stripe Connect account's status at fetch time. Mirrors the
/// fields persisted on <c>Organization</c> after an <c>account.updated</c>
/// webhook.
/// </summary>
public record StripeAccountStatus(
    string AccountId,
    bool ChargesEnabled,
    bool PayoutsEnabled,
    bool DetailsSubmitted,
    List<string> RequirementsCurrentlyDue);

/// <summary>
/// Wrapper for the Stripe Connect Express account-creation + onboarding-link
/// surface. Organization-scoped: each call relates to one Organization (which
/// owns one Stripe account), never to an individual BusinessUser.
/// </summary>
public interface IStripeConnectService
{
    /// <summary>
    /// Creates a new Express-type Connect account at Stripe and returns the
    /// new acct_... id. The caller is responsible for persisting the id on the
    /// Organization (typically via <c>sp_update_organization_stripe_account</c>).
    /// </summary>
    /// <param name="organizationId">Local Organization id — included as
    /// <c>organization_id</c> metadata on the Stripe account so dashboard /
    /// webhook payloads can be reconciled to the platform record.</param>
    /// <param name="contactEmail">Primary contact email (organizer's account
    /// email). Stripe uses this for required onboarding notifications.</param>
    /// <param name="countryCode">ISO 3166-1 alpha-2; drives which Stripe entity
    /// + capabilities are available.</param>
    Task<string> CreateExpressAccountAsync(Guid organizationId, string contactEmail, string countryCode);

    /// <summary>
    /// Generates a fresh AccountLink URL for the given account. URLs are
    /// short-lived (~5 minutes) — re-call this every time the admin/developer
    /// wants to resume onboarding.
    /// </summary>
    Task<string> CreateOnboardingLinkAsync(string stripeAccountId, OnboardingLinkScope scope);

    /// <summary>
    /// Pulls the live status payload from Stripe's <c>GET /v1/accounts/{id}</c>.
    /// Used by status endpoints + as a sanity check before generating a new
    /// onboarding link.
    /// </summary>
    Task<StripeAccountStatus> FetchAccountStatusAsync(string stripeAccountId);
}
