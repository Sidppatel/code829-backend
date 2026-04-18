namespace Contracts.DTOs.Venues;

public record VenueDto(
    Guid VenueId,
    string Name,
    string Address,
    string City,
    string State,
    string ZipCode,
    string? Description,
    string? ImageUrl,
    string? Phone,
    string? Email,
    string? Website,
    bool IsActive,
    DateTime CreatedAt
);
