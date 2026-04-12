namespace Contracts.DTOs.Events;

public record EventTicketTypeDto(
    Guid Id,
    string Label,
    int PriceCents,
    int? PlatformFeeCents,
    int? MaxQuantity,
    int SortOrder,
    bool IsActive,
    int SoldCount,
    int AvailableCount
);

public record CreateEventTicketTypeRequest(
    string Label,
    int PriceCents,
    int? PlatformFeeCents = null,
    int? MaxQuantity = null,
    int SortOrder = 0
);

public record UpdateEventTicketTypeRequest(
    string? Label = null,
    int? PriceCents = null,
    int? PlatformFeeCents = null,
    int? MaxQuantity = null,
    int? SortOrder = null,
    bool? IsActive = null
);

public record EventTicketTypesResponse(
    Guid EventId,
    List<EventTicketTypeDto> TicketTypes
);
