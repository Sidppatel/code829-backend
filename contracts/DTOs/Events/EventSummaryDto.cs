namespace Contracts.DTOs.Events;

public record EventSummaryDto(
    Guid Id,
    string Title,
    string Slug,
    string Status,
    string Category,
    DateTime StartDate,
    DateTime EndDate,
    string? ImageUrl,
    bool IsFeatured,
    string LayoutMode,
    string VenueName,
    string VenueCity,
    string VenueState,
    int? PricePerPersonCents,
    int TotalCapacity,
    int TotalSold,
    int NoOfAvailableTables,
    int? MinPricePerTableCents,
    int? MinTicketTypePriceCents = null,
    int? DisplayPricePerPersonCents = null,
    int? DisplayMinPricePerTableCents = null,
    int? DisplayMinTicketTypePriceCents = null
);
