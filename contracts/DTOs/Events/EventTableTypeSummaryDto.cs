namespace Contracts.DTOs.Events;

public record EventTableTypeSummaryDto(
    [property: System.Text.Json.Serialization.JsonPropertyName("id")] Guid EventTableId,
    string Label,
    int Capacity,
    string Shape,
    string? Color,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? PriceCents,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? PlatformFeeCents,
    int DisplayPriceCents,
    int TotalTables,
    int AvailableTables,
    int BookedTables
);

public record AdminEventTableTypeSummaryDto(
    [property: System.Text.Json.Serialization.JsonPropertyName("id")] Guid EventTableId,
    string Label,
    int Capacity,
    string Shape,
    string? Color,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? PriceCents,
    int? PlatformFeeCents,
    int DisplayPriceCents,
    int TotalTables,
    int AvailableTables,
    int BookedTables
);
