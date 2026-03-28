namespace Contracts.DTOs.Brand;

public record BrandConfigResponse(
    string Name,
    string Tagline,
    string PrimaryColor,
    string AccentColor,
    string DefaultTheme,
    string DefaultCity,
    string DefaultState,
    string DefaultTimezone
);
