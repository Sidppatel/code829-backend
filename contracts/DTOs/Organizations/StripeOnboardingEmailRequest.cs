namespace Contracts.DTOs.Organizations;

/// <summary>
/// Body for POST /developer/organizations/{id}/stripe-onboarding-email.
/// The BusinessUser referenced must already be a member of the organization
/// in the route — the service does not silently attach them.
/// </summary>
public record StripeOnboardingEmailRequest(Guid BusinessUserId);
