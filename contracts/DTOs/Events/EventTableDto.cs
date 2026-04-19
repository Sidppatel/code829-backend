namespace Contracts.DTOs.Events;

public record EventTableDto(
    [property: System.Text.Json.Serialization.JsonPropertyName("id")] Guid TableId,
    string Label,
    int Capacity,
    string Shape,
    string? Color,
    int PriceCents,
    int DisplayPriceCents,
    int GridRow,
    int GridCol,
    int SortOrder,
    string Status,
    DateTime? HoldExpiresAt,
    bool IsAvailable,
    bool IsLockedByYou = false,
    Guid? EventTableId = null,
    string? EventTableLabel = null
);

public record EventTableTypeInfo(
    Guid EventTableId,
    string Label,
    int Capacity,
    string Shape,
    string? Color,
    int PriceCents,
    int DisplayPriceCents,
    int? PlatformFeeCents = null
);

public record EventTablesResponse(
    Guid EventId,
    int? GridRows,
    int? GridCols,
    List<EventTableTypeInfo> EventTableTypes,
    List<EventTableDto> Tables
);
