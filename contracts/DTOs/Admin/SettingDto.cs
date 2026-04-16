namespace Contracts.DTOs.Admin;

public record SettingDto(
    string Key,
    string Value,
    string? Description,
    DateTime UpdatedAt
);

public record SecretStatusDto(
    string Key,
    bool Configured,
    string? Description
);

public record SettingsResponse(
    List<SettingDto> Settings,
    List<SecretStatusDto> Secrets
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
