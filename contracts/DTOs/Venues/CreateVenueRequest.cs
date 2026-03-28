namespace Contracts.DTOs.Venues;

public record CreateVenueRequest(
    string Name,
    string Address,
    string City,
    string State,
    string ZipCode,
    int Capacity,
    string? Description = null,
    string? Phone = null,
    string? Website = null,
    double? Latitude = null,
    double? Longitude = null
);
