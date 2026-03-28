namespace Contracts.DTOs.Events;

public record UpdateEventRequest(
    string? Title = null,
    string? Description = null,
    string? Category = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    Guid? VenueId = null,
    bool? IsFeatured = null,
    string? Status = null
);
