namespace Contracts.DTOs.Events;

public record EventPricingTierDto(
    string Name,
    int PriceCents,
    int? Capacity,
    int Count,
    int SoldCount,
    int? TotalCapacity,
    string? Description = null
);
