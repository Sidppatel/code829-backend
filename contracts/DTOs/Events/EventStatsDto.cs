namespace Contracts.DTOs.Events;

public record EventStatsDto(
    int TotalSold,
    int MaxCapacity,
    int FillRatePct,
    long GrossRevenueCents);
