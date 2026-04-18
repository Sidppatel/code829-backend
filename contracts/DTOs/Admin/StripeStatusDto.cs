namespace Contracts.DTOs.Admin;

public record StripeStatusDto(
    StripeKeyStatus SecretKey,
    StripeKeyStatus PublishableKey,
    StripeKeyStatus WebhookSecret,
    bool TaxEnabled,
    bool Verified,
    string? VerificationError,
    StripeAccountInfo? Account
);

public record StripeKeyStatus(
    bool Configured,
    string Mode,
    string Masked
);

public record StripeAccountInfo(
    string StripeAccountId,
    string? BusinessName,
    string? Country,
    bool ChargesEnabled,
    bool PayoutsEnabled,
    bool DetailsSubmitted
);
