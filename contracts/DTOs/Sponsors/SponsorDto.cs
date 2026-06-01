namespace Contracts.DTOs.Sponsors;

public record SponsorDto(
    Guid Id,
    string Name,
    string Slug,
    string? PrimaryImagePath,
    string? PrimaryImageUrl,
    IReadOnlyList<SponsorMetaItemDto> Meta,
    int EventCount,
    int UpcomingEventCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record SponsorSummaryDto(
    Guid Id,
    string Name,
    string Slug,
    string? PrimaryImageUrl,
    int EventCount,
    int UpcomingEventCount
);

public record EventSponsorDto(
    Guid SponsorId,
    string Name,
    string Slug,
    string? PrimaryImageUrl,
    int SortOrder,
    IReadOnlyList<SponsorMetaItemDto> EffectiveMeta
);
