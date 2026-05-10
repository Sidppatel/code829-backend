using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Contracts.DTOs.Events;

public record UpdateEventRequest(
    string? Title = null,
    string? Description = null,
    string? Category = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    Guid? VenueId = null,
    bool? IsFeatured = null,
    string? Status = null,
    string? LayoutMode = null,
    int? MaxCapacity = null,
    int? PricePerPersonCents = null,
    string? BannerImageUrl = null,
    List<NestedTicketTypeUpdate>? TicketTypes = null
);

public record NestedTicketTypeUpdate(
    [property: JsonPropertyName("eventTicketTypeId")] Guid? EventTicketTypeId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("priceCents")] int PriceCents,
    [property: JsonPropertyName("capacity")] int? Capacity,
    [property: JsonPropertyName("description")] string? Description = null
);
