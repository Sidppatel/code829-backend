namespace Contracts.DTOs.Performers;

public record CreatePerformerRequest(
    string Name,
    string? Slug = null,
    string? PrimaryImagePath = null,
    IReadOnlyList<PerformerMetaItemDto>? Meta = null
);

public record UpdatePerformerRequest(
    string? Name = null,
    string? Slug = null,
    string? PrimaryImagePath = null,
    IReadOnlyList<PerformerMetaItemDto>? Meta = null
);

public record EventPerformerLinkDto(
    Guid PerformerId,
    int SortOrder,
    IReadOnlyList<PerformerMetaItemDto>? EventMeta = null
);

public record SetEventPerformersRequest(
    IReadOnlyList<EventPerformerLinkDto> Performers
);
