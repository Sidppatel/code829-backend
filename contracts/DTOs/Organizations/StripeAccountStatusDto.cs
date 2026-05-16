namespace Contracts.DTOs.Organizations;

public record StripeAccountStatusDto(
    string AccountId,
    bool ChargesEnabled,
    bool PayoutsEnabled,
    bool DetailsSubmitted,
    IReadOnlyList<string> RequirementsCurrentlyDue
);
