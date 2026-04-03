namespace Contracts.DTOs.Events;

public record CreateEventRequest(
    string Title,
    string? Description,
    string Category,
    DateTime StartDate,
    DateTime EndDate,
    Guid VenueId,
    string LayoutMode,
    bool IsFeatured = false,
    int? MaxCapacity = null,
    int? PricePerPersonCents = null,
    int? PlatformFeePercent = null,
    string? BannerImageUrl = null
);
