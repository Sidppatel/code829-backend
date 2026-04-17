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
    int TotalCapacity,
    int TotalSold,
    int NoOfAvailableTables,
    int? DisplayFromAmountCents,
    string? DisplayFromFormatted,
    bool IsSoldOut,
    int AvailableCount
);
