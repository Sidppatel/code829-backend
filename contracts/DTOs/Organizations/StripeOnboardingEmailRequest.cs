namespace Contracts.DTOs.Organizations;

public record StripeOnboardingEmailRequest(
    Guid? BusinessUserId = null,
    string? RecipientEmail = null);
