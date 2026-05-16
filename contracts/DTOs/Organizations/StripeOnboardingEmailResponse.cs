namespace Contracts.DTOs.Organizations;

public record StripeOnboardingEmailResponse(
    Guid EmailLogId,
    string RecipientEmail);
