namespace Contracts.DTOs.Seats;

public record CreateTableTypeRequest(
    string Name,
    int SeatsPerTable,
    string Shape,
    int WidthPx = 80,
    int HeightPx = 80
);

public record CreateTableRequest(
    string Label,
    double X,
    double Y,
    double Rotation,
    Guid TableTypeId
);

public record UpdateTableRequest(
    string? Label = null,
    double? X = null,
    double? Y = null,
    double? Rotation = null
);

public record TableTypeDto(
    Guid Id,
    string Name,
    int SeatsPerTable,
    string Shape,
    int WidthPx,
    int HeightPx
);
