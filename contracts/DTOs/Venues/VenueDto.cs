namespace Contracts.DTOs.Venues;

public record VenueDto(
    Guid Id,
    string Name,
    string Address,
    string City,
    string State,
    string ZipCode,
    string? Description,
    string? ImageUrl,
    string? Phone,
    string? Website,
    bool IsActive,
    DateTime CreatedAt
);
