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
    int? DisplayFromAmountCents,
    string? DisplayFromFormatted,
    bool IsSoldOut,
    int AvailableCount,
    List<EventTicketTypeDto>? TicketTypes = null,
    List<EventTableTypeSummaryDto>? TableTypes = null,
    // Raw (pre-fee) price for admin surfaces. Not populated on public responses.
    int? PricePerPersonCents = null
);
