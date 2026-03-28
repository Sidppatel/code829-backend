namespace Contracts.DTOs.Seats;

public record SeatDto(
    Guid Id,
    string Label,
    int SeatNumber,
    Guid TableId,
    string TableLabel,
    bool IsHeld,
    Guid? HeldByUserId
);

public record TableDto(
    Guid Id,
    string Label,
    double X,
    double Y,
    double Rotation,
    string TableTypeName,
    string Shape,
    int WidthPx,
    int HeightPx,
    List<SeatDto> Seats
);

public record VenueLayoutDto(
    Guid VenueId,
    string VenueName,
    List<TableDto> Tables
);
