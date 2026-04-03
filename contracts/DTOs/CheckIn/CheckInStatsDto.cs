namespace Contracts.DTOs.CheckIn;

public record CheckInStatsDto(
    Guid EventId,
    string EventTitle,
    int TotalTicketsSold,
    int CheckedIn,
    int Pending,
    int Remaining,
    double Percentage,
    DateTime? LastCheckIn
);
