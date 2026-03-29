namespace Contracts.DTOs.Events;

public record CreateEventRequest(
    string Title,
    string? Description,
    string Category,
    DateTime StartDate,
    DateTime EndDate,
    Guid VenueId,
    bool IsFeatured = false,
    string? LayoutMode = null,
    int? MaxCapacity = null,
    int? PlatformFeePercent = null,
    string? BannerImageUrl = null,
    List<CreateTicketTypeRequest>? TicketTypes = null
);

public record CreateTicketTypeRequest(
    string Name,
    string? Description,
    int PriceCents,
    int QuantityTotal,
    int SortOrder = 0,
    int PlatformFeeCents = 0
);
