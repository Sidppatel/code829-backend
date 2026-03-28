namespace Contracts.DTOs.CheckIn;

public record CheckInStatsDto(
    Guid EventId,
    string EventTitle,
    int TotalBookings,
    int CheckedIn,
    int Pending,
    int Paid,
    double CheckInPercentage,
    int TotalTicketsSold,
    DateTime? LastCheckInAt
);
