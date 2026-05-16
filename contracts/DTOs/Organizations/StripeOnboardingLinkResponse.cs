namespace Contracts.DTOs.Organizations;

public record StripeOnboardingLinkResponse(string Url, DateTime ExpiresAt);
