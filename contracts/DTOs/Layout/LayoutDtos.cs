namespace Contracts.DTOs.Layout;

public record TableTemplateResponse(
    Guid TableTemplateId,
    string Name,
    int DefaultCapacity,
    string DefaultShape,
    string? DefaultColor,
    int DefaultPriceCents,
    bool IsActive,
    int DefaultRowSpan = 1,
    int DefaultColSpan = 1
);

public record CreateTableTemplateRequest(
    string Name,
    int DefaultCapacity,
    string DefaultShape,
    string? DefaultColor = null,
    int DefaultPriceCents = 0,
    bool? IsActive = null,
    int DefaultRowSpan = 1,
    int DefaultColSpan = 1
);

public record EventTableResponse(
    [property: System.Text.Json.Serialization.JsonPropertyName("id")] Guid EventTableId,
    string Label,
    int Capacity,
    string Shape,
    string? Color,
    int PriceCents,
    bool IsActive,
    Guid EventId,
    Guid? TableTemplateId,
    string? TableTemplateName,
    int TableCount,
    int? RowSpan = null,
    int? ColSpan = null
);

public record CreateEventTableRequest(
    Guid? TableTemplateId = null,
    string? Label = null,
    int? Capacity = null,
    string? Shape = null,
    string? Color = null,
    int? PriceCents = null,
    int? RowSpan = null,
    int? ColSpan = null
);

public record UpdateEventTableRequest(
    string? Label = null,
    int? Capacity = null,
    string? Shape = null,
    string? Color = null,
    int? PriceCents = null,
    bool? IsActive = null,
    int? RowSpan = null,
    int? ColSpan = null
);

public record EventLayoutResponse(
    Guid EventId,
    int? GridRows,
    int? GridCols,
    List<LayoutTableResponse> Tables
);

public record LayoutTableResponse(
    [property: System.Text.Json.Serialization.JsonPropertyName("id")] Guid TableId,
    string Label,
    int GridRow,
    int GridCol,
    bool IsActive,
    int SortOrder,
    Guid EventTableId,
    string EventTableLabel,
    int Capacity,
    string Shape,
    string? Color,
    int PriceCents,
    string Status = "Available",
    int RowSpan = 1,
    int ColSpan = 1
);

public record SaveLayoutRequest(
    int? GridRows,
    int? GridCols,
    List<SaveLayoutTableRequest> Tables
);

public record SaveLayoutTableRequest(
    string? Id,
    string Label,
    int GridRow,
    int GridCol,
    bool IsActive,
    int SortOrder,
    Guid EventTableId,
    int RowSpan = 1,
    int ColSpan = 1
);

public record AddTableRequest(
    string Label,
    int GridRow,
    int GridCol,
    Guid EventTableId,
    int RowSpan = 1,
    int ColSpan = 1
);

public record UpdateTableRequest(
    string? Label = null,
    int? GridRow = null,
    int? GridCol = null,
    bool? IsActive = null,
    int? SortOrder = null,
    Guid? EventTableId = null,
    int? RowSpan = null,
    int? ColSpan = null
);

public record LayoutStatsResponse(
    int TotalTables,
    int TotalCapacity,
    long TotalPotentialRevenueCents,
    long TotalBookedRevenueCents
);

public record BulkInsertRequest(List<Guid> TableTemplateIds);

public record BulkInsertResponse(
    int InsertedCount,
    List<EventTableResponse> EventTables
);
