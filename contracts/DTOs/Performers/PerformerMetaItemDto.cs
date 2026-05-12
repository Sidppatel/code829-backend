namespace Contracts.DTOs.Performers;

public record PerformerMetaItemDto(
    string Key,
    string? Value,
    bool IsPublic,
    int SortOrder
);
