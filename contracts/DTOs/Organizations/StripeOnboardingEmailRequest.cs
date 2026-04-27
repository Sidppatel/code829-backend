namespace Contracts.DTOs.Organizations;

/// <summary>
/// Body for POST /developer/organizations/{id}/stripe-onboarding-email.
/// Provide either:
/// <list type="bullet">
///   <item><see cref="BusinessUserId"/> — must be a member of the organization in the route. The service emails that member's recorded address.</item>
///   <item><see cref="RecipientEmail"/> — arbitrary contact address (e.g. an organizer not yet attached to the org). Use this when the developer is bootstrapping an org for someone whose account does not exist on the platform yet.</item>
/// </list>
/// At least one must be supplied. <c>RecipientEmail</c> wins when both are present so the developer can override.
/// </summary>
public record StripeOnboardingEmailRequest(
    Guid? BusinessUserId = null,
    string? RecipientEmail = null);
