namespace Contracts.DTOs.Venues;

public record CreateVenueRequest(
    string Name,
    string Address,
    string City,
    string State,
    string ZipCode,
    string? Description = null,
    string? Phone = null,
    string? Email = null,
    string? Website = null
);
