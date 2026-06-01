namespace Contracts.DTOs.Sponsors;

public record CreateSponsorRequest(
    string Name,
    string? Slug = null,
    string? PrimaryImagePath = null,
    IReadOnlyList<SponsorMetaItemDto>? Meta = null
);

public record UpdateSponsorRequest(
    string? Name = null,
    string? Slug = null,
    string? PrimaryImagePath = null,
    IReadOnlyList<SponsorMetaItemDto>? Meta = null
);

public record EventSponsorLinkDto(
    Guid SponsorId,
    int SortOrder,
    IReadOnlyList<SponsorMetaItemDto>? EventMeta = null
);

public record SetEventSponsorsRequest(
    IReadOnlyList<EventSponsorLinkDto> Sponsors
);
