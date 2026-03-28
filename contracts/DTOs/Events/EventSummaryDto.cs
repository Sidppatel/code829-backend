namespace Contracts.DTOs.Events;

/// <summary>
/// Lightweight event DTO for listing pages — omits full description and nested venue details.
/// </summary>
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
    string VenueName,
    string VenueCity,
    string VenueState,
    int? MinPriceCents,
    int? MaxPriceCents,
    int TotalCapacity,
    int TotalSold
);
