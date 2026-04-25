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
    List<string> RequirementsCurrentlyDue,
    string? BankAccountLast4,
    string? DisabledReason);

/// <summary>
/// Prefilled <c>business_profile</c> + <c>business_type</c> values applied at
/// account-creation time so Stripe's hosted onboarding skips the
/// "Business details" screen (industry / website / product description).
/// KYC identity collection still runs — only the business-info step collapses.
/// </summary>
/// <param name="LegalName">Org legal name. Mirrored to
/// <c>business_profile.name</c>.</param>
/// <param name="ProductDescription">10-500 char description shown to Stripe in
/// place of a website URL. Required for accounts without their own site.</param>
/// <param name="Mcc">4-digit Merchant Category Code. Defaults to
/// <c>"7922"</c> (Theatrical Producers / Ticket Agencies) at the controller.</param>
/// <param name="BusinessType"><c>"individual"</c> or <c>"company"</c>.
/// Routes Stripe to the matching identity questionnaire.</param>
public record StripeBusinessProfilePrefill(
    string LegalName,
    string ProductDescription,
    string Mcc,
    string BusinessType);

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
    /// <param name="prefill">Business-profile fields written at create time so
    /// Stripe's hosted onboarding skips the Business Details screen. See
    /// <see cref="StripeBusinessProfilePrefill"/>.</param>
    Task<string> CreateExpressAccountAsync(Guid organizationId, string contactEmail, string countryCode, StripeBusinessProfilePrefill prefill);

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

    /// <summary>
    /// Mints a one-shot Express dashboard URL for a connected account. Stripe
    /// LoginLinks expire ~5 minutes after creation; never cache.
    /// Returns null when the account is not yet payouts-enabled (Stripe rejects
    /// LoginLink for accounts without complete onboarding).
    /// </summary>
    Task<string?> CreateLoginLinkAsync(string stripeAccountId);

    /// <summary>
    /// Permanently deletes a connected account at Stripe so a fresh onboarding
    /// flow can begin. Test-mode accounts can always be deleted; live-mode
    /// Express / Custom accounts can only be deleted when balances are zero
    /// (Stripe enforces this and returns a typed error otherwise).
    ///
    /// <para>Returns true on successful deletion (or when Stripe reports the
    /// account already absent). Throws via <c>MapStripeException</c> on hard
    /// failures (e.g. live-mode non-zero balance) so the controller can surface
    /// the constraint to the caller.</para>
    /// </summary>
    Task<bool> DeleteAccountAsync(string stripeAccountId);
}
