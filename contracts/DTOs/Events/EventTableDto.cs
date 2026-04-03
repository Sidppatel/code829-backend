namespace Contracts.DTOs.Events;

public record EventTableDto(
    Guid Id,
    string Label,
    int Capacity,
    string Shape,
    string? Color,
    int PriceCents,
    double PosX,
    double PosY,
    int SortOrder,
    string Status,
    DateTime? HoldExpiresAt,
    bool IsLockedByYou = false
);

public record EventTablesResponse(
    Guid EventId,
    int? GridRows,
    int? GridCols,
    List<EventTableDto> Tables
);
