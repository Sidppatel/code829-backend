namespace Contracts.DTOs.Organizations;

/// <summary>
/// Body for POST /developer/organizations/{id}/stripe-onboarding-link
/// and /admin/organization/stripe-resume-link.
///
/// Scope values:
///   - "identity" — first onboarding link, captures KYC details only.
///                  Bank step is skipped (per Stripe dashboard "Require at
///                  least one bank account = No" toggle).
///   - "bank"     — second onboarding link, opens just the external_account
///                  collection step. Used after identity is verified.
/// </summary>
public record StripeOnboardingLinkRequest(string Scope);
