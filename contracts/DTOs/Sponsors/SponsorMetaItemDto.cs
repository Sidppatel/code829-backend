namespace Contracts.DTOs.Sponsors;

public record SponsorMetaItemDto(
    string Key,
    string? Value,
    bool IsPublic,
    int SortOrder
);
