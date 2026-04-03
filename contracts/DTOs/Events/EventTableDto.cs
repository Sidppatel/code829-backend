namespace Contracts.DTOs.Events;

public record EventTableDto(
    Guid Id,
    string Label,
    int Capacity,
    string Shape,
    string? Color,
    int PriceCents,
    int GridRow,
    int GridCol,
    int SortOrder,
    string Status,
    DateTime? HoldExpiresAt,
    bool IsLockedByYou = false,
    Guid? EventTableId = null,
    string? EventTableLabel = null
);

public record EventTableTypeInfo(
    Guid Id,
    string Label,
    int Capacity,
    string Shape,
    string? Color,
    int PriceCents
);

public record EventTablesResponse(
    Guid EventId,
    int? GridRows,
    int? GridCols,
    List<EventTableTypeInfo> EventTableTypes,
    List<EventTableDto> Tables
);
