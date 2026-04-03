namespace Contracts.DTOs.Layout;

// ─── Table Templates (global) ────────────────────────────────

public record TableTemplateResponse(
    Guid Id,
    string Name,
    int DefaultCapacity,
    string DefaultShape,
    string? DefaultColor,
    int DefaultPriceCents,
    bool IsActive
);

public record CreateTableTemplateRequest(
    string Name,
    int DefaultCapacity,
    string DefaultShape,
    string? DefaultColor = null,
    int DefaultPriceCents = 0
);

// ─── Event Tables (per-event table types) ────────────────────

public record EventTableResponse(
    Guid Id,
    string Label,
    int Capacity,
    string Shape,
    string? Color,
    int PriceCents,
    bool IsActive,
    Guid EventId,
    Guid? TableTemplateId,
    string? TableTemplateName,
    int TableCount
);

public record CreateEventTableRequest(
    Guid TableTemplateId,
    string? Label = null,
    int? Capacity = null,
    string? Shape = null,
    string? Color = null,
    int? PriceCents = null
);

public record UpdateEventTableRequest(
    string? Label = null,
    int? Capacity = null,
    string? Shape = null,
    string? Color = null,
    int? PriceCents = null,
    bool? IsActive = null
);

// ─── Layout (table instances on grid) ────────────────────────

public record EventLayoutResponse(
    Guid EventId,
    int? GridRows,
    int? GridCols,
    List<LayoutTableResponse> Tables
);

public record LayoutTableResponse(
    Guid Id,
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
    string Status = "Available"
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
    Guid EventTableId
);

public record AddTableRequest(
    string Label,
    int GridRow,
    int GridCol,
    Guid EventTableId
);

public record UpdateTableRequest(
    string? Label = null,
    int? GridRow = null,
    int? GridCol = null,
    bool? IsActive = null,
    int? SortOrder = null,
    Guid? EventTableId = null
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
