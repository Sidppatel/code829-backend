namespace Contracts.DTOs.Events;

public record CreateEventRequest(
    string Title,
    string? Description,
    string Category,
    DateTime StartDate,
    DateTime EndDate,
    Guid VenueId,
    bool IsFeatured = false,
    List<CreateTicketTypeRequest>? TicketTypes = null
);

public record CreateTicketTypeRequest(
    string Name,
    string? Description,
    int PriceCents,
    int QuantityTotal,
    int SortOrder = 0
);
