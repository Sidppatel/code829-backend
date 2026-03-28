namespace Contracts.DTOs.Venues;

public record VenueDto(
    Guid Id,
    string Name,
    string Address,
    string City,
    string State,
    string ZipCode,
    int Capacity,
    string? Description,
    string? ImageUrl,
    string? Phone,
    string? Website,
    double? Latitude,
    double? Longitude,
    bool IsActive,
    DateTime CreatedAt
);
