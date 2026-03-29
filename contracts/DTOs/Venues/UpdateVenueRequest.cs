namespace Contracts.DTOs.Venues;

public record UpdateVenueRequest(
    string? Name = null,
    string? Address = null,
    string? City = null,
    string? State = null,
    string? ZipCode = null,
    string? Description = null,
    string? Phone = null,
    string? Email = null,
    string? Website = null,
    bool? IsActive = null
);
