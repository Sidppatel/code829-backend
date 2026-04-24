namespace Contracts.DTOs.Organizations;

/// <summary>
/// Response from any onboarding-link-creation endpoint. Url is the Stripe-hosted
/// page URL; ExpiresAt is when the link stops working (Stripe AccountLink URLs
/// are short-lived, typically ~5 minutes).
/// </summary>
public record StripeOnboardingLinkResponse(string Url, DateTime ExpiresAt);
