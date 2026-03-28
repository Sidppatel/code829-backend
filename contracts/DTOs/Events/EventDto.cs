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
    int? PlatformFeePercent,
    DateTime? PublishedAt,
    Guid VenueId,
    VenueDto? Venue,
    Guid OrganizerId,
    string? OrganizerName,
    List<TicketTypeDto> TicketTypes,
    DateTime CreatedAt
);
