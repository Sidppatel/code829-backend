namespace Contracts.DTOs.Layout;

public record TableTypeResponse(
    Guid Id,
    string Name,
    int DefaultCapacity,
    string DefaultShape,
    string? DefaultColor,
    int DefaultPriceCents,
    bool IsActive
);

public record CreateTableTypeRequest(
    string Name,
    int DefaultCapacity,
    string DefaultShape,
    string? DefaultColor = null,
    int DefaultPriceCents = 0
);

public record EventLayoutResponse(
    Guid EventId,
    int? GridRows,
    int? GridCols,
    List<LayoutTableResponse> Tables
);

public record LayoutTableResponse(
    Guid Id,
    string Label,
    int Capacity,
    string Shape,
    string? Color,
    int PriceCents,
    bool IsActive,
    double PosX,
    double PosY,
    int SortOrder,
    Guid? TableTypeId,
    string? TableTypeName
);

public record SaveLayoutRequest(
    int? GridRows,
    int? GridCols,
    List<SaveLayoutTableRequest> Tables
);

public record SaveLayoutTableRequest(
    string? Id,
    string Label,
    int Capacity,
    string Shape,
    string? Color,
    int PriceCents,
    bool IsActive,
    double PosX,
    double PosY,
    int SortOrder,
    Guid? TableTypeId
);

public record AddTableRequest(
    string Label,
    int Capacity,
    string Shape,
    string? Color = null,
    int PriceCents = 0,
    double PosX = 0,
    double PosY = 0,
    Guid? TableTypeId = null
);

public record UpdateTableRequest(
    string? Label = null,
    int? Capacity = null,
    string? Shape = null,
    string? Color = null,
    int? PriceCents = null,
    bool? IsActive = null,
    double? PosX = null,
    double? PosY = null,
    int? SortOrder = null
);

public record LayoutStatsResponse(
    int TotalTables,
    int TotalCapacity,
    long TotalPotentialRevenueCents,
    long TotalBookedRevenueCents
);

public record BulkInsertRequest(List<Guid> TableTypeIds);

public record BulkInsertResponse(
    int InsertedCount,
    List<LayoutTableResponse> Tables
);
