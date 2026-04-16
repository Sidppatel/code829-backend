namespace Contracts.DTOs.Events;

public record EventTableTypeSummaryDto(
    Guid Id,
    string Label,
    int Capacity,
    string Shape,
    string? Color,
    int PriceCents,
    int? PlatformFeeCents,
    int DisplayPriceCents,
    int TotalTables,
    int AvailableTables,
    int BookedTables
);
