namespace Contracts.DTOs.Organizations;

public record StartStripeOnboardingRequest(
    string BusinessType,
    string? LegalName = null,
    string? ProductDescription = null,
    string? Mcc = null);
