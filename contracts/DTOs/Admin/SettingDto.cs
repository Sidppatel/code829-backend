namespace Contracts.DTOs.Admin;

public record SettingDto(
    string Key,
    string MaskedValue,
    string? Description,
    DateTime UpdatedAt
);

public record UpdateSettingRequest(
    string Key,
    string Value
);

public record UpdateStripeKeysRequest(
    string? SecretKey = null,
    string? PublishableKey = null,
    string? WebhookSecret = null,
    bool? TaxEnabled = null
);
