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
    int? PlatformFeePercent,
    int? PlatformFeeCents,
    int? GridRows,
    int? GridCols,
    DateTime? PublishedAt,
    Guid VenueId,
    VenueDto? Venue,
    Guid OrganizerId,
    string? OrganizerName,
    DateTime CreatedAt,
    int TotalCapacity,
    int TotalSold,
    int NoOfAvailableTables,
    int? MinPricePerTableCents,
    int? MinTicketTypePriceCents = null,
    List<EventPricingTierDto>? PricingTiers = null,
    List<EventTicketTypeDto>? TicketTypes = null
);
