using System.Collections.Generic;
using System.Text.Json.Serialization;

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
    string? BannerImageUrl = null,
    List<NestedTicketTypeRequest>? TicketTypes = null
);

public record NestedTicketTypeRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("priceCents")] int PriceCents,
    [property: JsonPropertyName("capacity")] int? Capacity,
    [property: JsonPropertyName("description")] string? Description = null
);
