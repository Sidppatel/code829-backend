namespace Contracts.DTOs.Organizations;

public record OrganizationDetailDto(
    Guid Id,
    string Name,
    string? LegalName,
    string CountryCode,
    string? StripeConnectedAccountId,
    bool StripeChargesEnabled,
    bool StripePayoutsEnabled,
    bool StripeDetailsSubmitted,
    DateTime? StripeOnboardedAt,
    string? StripeRequirementsDue,
    DateTime? ArchivedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<OrganizationMemberDto> Members
);
