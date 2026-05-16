namespace Contracts.DTOs.Organizations;

public record OrganizationListItemDto(
    Guid Id,
    string Name,
    string? LegalName,
    string CountryCode,
    string? StripeConnectedAccountId,
    bool StripeChargesEnabled,
    bool StripePayoutsEnabled,
    bool StripeDetailsSubmitted,
    DateTime? StripeOnboardedAt,
    DateTime? ArchivedAt,
    int MemberCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
        string StripeState,
        bool HasStripeAccount
);
