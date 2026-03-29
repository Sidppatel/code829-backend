namespace Contracts.DTOs.Events;

public record EventTableDto(
    Guid Id,
    string Label,
    int Capacity,
    string Shape,
    string? Color,
    string? Section,
    string PriceType,
    int PriceCents,
    int PlatformFeeCents,
    int? GridRow,
    int? GridCol,
    int SortOrder,
    string Status,
    DateTime? HoldExpiresAt
);

public record EventTablesResponse(
    Guid EventId,
    int GridRows,
    int GridCols,
    List<EventTableDto> Tables
);
