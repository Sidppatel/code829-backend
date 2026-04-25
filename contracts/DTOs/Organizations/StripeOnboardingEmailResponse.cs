namespace Contracts.DTOs.Organizations;

/// <summary>
/// Response for POST /developer/organizations/{id}/stripe-onboarding-email.
/// Surfaces the email_logs row id (audit reference) and the resolved
/// recipient email so the FE can render a confirmation toast like
/// "Sent to alice@example.com" without a second lookup.
/// </summary>
public record StripeOnboardingEmailResponse(
    Guid EmailLogId,
    string RecipientEmail);
