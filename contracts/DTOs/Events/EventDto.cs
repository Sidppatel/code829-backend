using Contracts.DTOs.Venues;

namespace Contracts.DTOs.Events;

public record EventDto(
    Guid Id,
    string Title,
    string Slug,
    string? Description,
    string Status,
    string Category,
    DateTime StartDate,
    DateTime EndDate,
    string? ImageUrl,
    bool IsFeatured,
    string LayoutMode,
    int? MaxCapacity,
    int? PricePerPersonCents,
    int? GridRows,
    int? GridCols,
    DateTime? PublishedAt,
    Guid VenueId,
    string? VenueName,
    VenueDto? Venue,
    Guid OrganizerId,
    string? OrganizerName,
    DateTime CreatedAt,
    int TotalCapacity,
    int TotalSold,
    int NoOfAvailableTables,
    int? MinPricePerTableCents,
    int? MinTicketTypePriceCents = null,
    int? DisplayPricePerPersonCents = null,
    int? DisplayMinPricePerTableCents = null,
    int? DisplayMinTicketTypePriceCents = null,
    List<EventTicketTypeDto>? TicketTypes = null,
    List<EventTableTypeSummaryDto>? TableTypes = null
);
