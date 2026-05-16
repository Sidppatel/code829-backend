namespace Contracts.DTOs.Organizations;

public record OrganizationMemberDto(
    Guid BusinessUserId,
    string Email,
    string? DisplayName
);

public record OrganizationStripeStatusDto(
    Guid OrganizationId,
    string OrganizationName,
    StripeAccountStatusDto? StripeAccount,
    string State,
    string? BankAccountLast4,
    List<OrganizationMemberDto> Members,
    string? ExpressDashboardUrl,
    DateTime FetchedAt
);
