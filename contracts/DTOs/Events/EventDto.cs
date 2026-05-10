using Contracts.DTOs.Venues;

namespace Contracts.DTOs.Events;

public record EventDto(
    Guid EventId,
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
    int? GridRows,
    int? GridCols,
    DateTime? PublishedAt,
    Guid VenueId,
    string? VenueName,
    VenueDto? Venue,
    Guid BusinessUserId,
    string? OrganizerName,
    DateTime CreatedAt,
    int TotalCapacity,
    int TotalSold,
    int NoOfAvailableTables,
    int? DisplayFromAmountCents,
    string? DisplayFromFormatted,
    bool IsSoldOut,
    int AvailableCount,
    List<EventTicketTypeDto>? TicketTypes = null,
    List<EventTableTypeSummaryDto>? TableTypes = null,

    int? PricePerPersonCents = null,
    int TotalTables = 0,
    int BookedTables = 0
);
