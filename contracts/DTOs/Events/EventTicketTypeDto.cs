namespace Contracts.DTOs.Events;

public record EventTicketTypeDto(
    [property: System.Text.Json.Serialization.JsonPropertyName("id")] Guid EventTicketTypeId,
    string Label,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? PriceCents,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? PlatformFeeCents,
    int DisplayPriceCents,
    int? MaxQuantity,
    int SortOrder,
    bool IsActive,
    int SoldCount,
    int AvailableCount,
    bool IsSoldOut,
    string? Description = null
);

public record AdminEventTicketTypeDto(
    [property: System.Text.Json.Serialization.JsonPropertyName("id")] Guid EventTicketTypeId,
    string Label,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? PriceCents,
    int? PlatformFeeCents,
    int DisplayPriceCents,
    int? MaxQuantity,
    int SortOrder,
    bool IsActive,
    int SoldCount,
    int AvailableCount,
    bool IsSoldOut,
    string? Description = null
);

public record CreateEventTicketTypeRequest(
    string Label,
    int PriceCents,
    int? PlatformFeeCents = null,
    int? MaxQuantity = null,
    int SortOrder = 0,
    string? Description = null
);

public record UpdateEventTicketTypeRequest(
    string? Label = null,
    int? PriceCents = null,
    int? PlatformFeeCents = null,
    int? MaxQuantity = null,
    int? SortOrder = null,
    bool? IsActive = null,
    string? Description = null
);

public record EventTicketTypesResponse(
    Guid EventId,
    List<EventTicketTypeDto> TicketTypes
);

public record AdminEventTicketTypesResponse(
    Guid EventId,
    List<AdminEventTicketTypeDto> TicketTypes
);
