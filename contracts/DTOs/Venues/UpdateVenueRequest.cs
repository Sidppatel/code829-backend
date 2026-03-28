namespace Contracts.DTOs.Venues;

public record UpdateVenueRequest(
    string? Name = null,
    string? Address = null,
    string? City = null,
    string? State = null,
    string? ZipCode = null,
    int? Capacity = null,
    string? Description = null,
    string? Phone = null,
    string? Website = null,
    double? Latitude = null,
    double? Longitude = null,
    bool? IsActive = null
);
