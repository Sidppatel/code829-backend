namespace Contracts.DTOs.Events;

public record EventTableDto(
    [property: System.Text.Json.Serialization.JsonPropertyName("id")] Guid TableId,
    string Label,
    int Capacity,
    string Shape,
    string? Color,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? PriceCents,
    int DisplayPriceCents,
    int GridRow,
    int GridCol,
    int SortOrder,
    string Status,
    DateTime? HoldExpiresAt,
    bool IsAvailable,
    bool IsLockedByYou = false,
    Guid? EventTableId = null,
    string? EventTableLabel = null,
    int RowSpan = 1,
    int ColSpan = 1
);

public record EventTableTypeInfo(
    [property: System.Text.Json.Serialization.JsonPropertyName("id")] Guid EventTableId,
    string Label,
    int Capacity,
    string Shape,
    string? Color,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? PriceCents,
    int DisplayPriceCents,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? PlatformFeeCents = null,
    int DefaultRowSpan = 1,
    int DefaultColSpan = 1
);

public record EventTablesResponse(
    Guid EventId,
    int? GridRows,
    int? GridCols,
    List<EventTableTypeInfo> EventTableTypes,
    List<EventTableDto> Tables
);
