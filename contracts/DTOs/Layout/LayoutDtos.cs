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
    string? EditorMode,
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
    string? Section,
    string PriceType,
    int PriceCents,
    int? PriceOverrideCents,
    bool IsActive,
    int? GridRow,
    int? GridCol,
    double? PosX,
    double? PosY,
    double Width,
    double Height,
    double Rotation,
    int SortOrder,
    Guid? TableTypeId,
    string? TableTypeName
);

public record SaveLayoutRequest(
    string? EditorMode,
    int? GridRows,
    int? GridCols,
    List<SaveLayoutTableRequest> Tables
);

public record SaveLayoutTableRequest(
    Guid? Id,
    string Label,
    int Capacity,
    string Shape,
    string? Color,
    string? Section,
    string PriceType,
    int PriceCents,
    int? PriceOverrideCents,
    bool IsActive,
    int? GridRow,
    int? GridCol,
    double? PosX,
    double? PosY,
    double Width,
    double Height,
    double Rotation,
    int SortOrder,
    Guid? TableTypeId
);

public record AddTableRequest(
    string Label,
    int Capacity,
    string Shape,
    string? Color = null,
    string? Section = null,
    string PriceType = "PerSeat",
    int PriceCents = 0,
    int? GridRow = null,
    int? GridCol = null,
    double? PosX = null,
    double? PosY = null,
    double Width = 80,
    double Height = 80,
    double Rotation = 0,
    Guid? TableTypeId = null
);

public record UpdateTableRequest(
    string? Label = null,
    int? Capacity = null,
    string? Shape = null,
    string? Color = null,
    string? Section = null,
    string? PriceType = null,
    int? PriceCents = null,
    int? PriceOverrideCents = null,
    bool? IsActive = null,
    int? GridRow = null,
    int? GridCol = null,
    double? PosX = null,
    double? PosY = null,
    double? Width = null,
    double? Height = null,
    double? Rotation = null,
    int? SortOrder = null
);
