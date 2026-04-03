namespace Contracts.DTOs.Bookings;

public record BookingDto(
    Guid Id,
    string BookingNumber,
    string Status,
    Guid UserId,
    string UserName,
    Guid EventId,
    string EventTitle,
    int SubtotalCents,
    int FeeCents,
    int TotalCents,
    string? QrToken,
    Guid? TableId,
    string? TableLabel,
    int? SeatsReserved,
    PaymentDto? Payment,
    DateTime CreatedAt
);

public record PaymentDto(
    Guid Id,
    string PaymentIntentId,
    string Status,
    int AmountCents,
    DateTime? PaidAt,
    DateTime? RefundedAt
);
