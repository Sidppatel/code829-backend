namespace Contracts.DTOs.Performers;

public record PerformerDto(
    Guid Id,
    string Name,
    string Slug,
    string? PrimaryImagePath,
    string? PrimaryImageUrl,
    IReadOnlyList<PerformerMetaItemDto> Meta,
    int EventCount,
    int UpcomingEventCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record PerformerSummaryDto(
    Guid Id,
    string Name,
    string Slug,
    string? PrimaryImageUrl,
    int EventCount,
    int UpcomingEventCount
);

public record EventPerformerDto(
    Guid PerformerId,
    string Name,
    string Slug,
    string? PrimaryImageUrl,
    int SortOrder,
    IReadOnlyList<PerformerMetaItemDto> EffectiveMeta
);
