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
    List<BookingItemDto> Items,
    PaymentDto? Payment,
    DateTime CreatedAt
);

public record BookingItemDto(
    Guid Id,
    Guid TicketTypeId,
    string TicketTypeName,
    Guid? SeatId,
    string? SeatLabel,
    int PriceCents
);

public record PaymentDto(
    Guid Id,
    string PaymentIntentId,
    string Status,
    int AmountCents,
    DateTime? PaidAt,
    DateTime? RefundedAt
);
