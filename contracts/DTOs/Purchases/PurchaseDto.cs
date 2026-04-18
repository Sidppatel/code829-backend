namespace Contracts.DTOs.Purchases;

public record PurchaseDto(
    Guid PurchaseId,
    string PurchaseNumber,
    string Status,
    Guid UserId,
    string UserName,
    Guid EventId,
    string EventTitle,
    DateTime EventDate,
    DateTime? EventEndDate,
    string? EventCategory,
    string? EventImagePath,
    string? VenueName,
    string? VenueAddress,
    int SubtotalCents,
    int TotalCents,
    string? QrToken,
    Guid? TableId,
    string? TableLabel,
    int? SeatsReserved,
    Guid? EventTicketTypeId,
    string? EventTicketTypeLabel,
    int TicketCount,
    StripeTransactionDto? Transaction,
    DateTime CreatedAt,
    string? ClientSecret = null,
    int? FeeCents = null
);

public record StripeTransactionDto(
    Guid StripeTransactionId,
    string PaymentIntentId,
    string Status,
    int AmountCents,
    int? TotalChargedCents,
    int? TaxAmountCents,
    int? StripeFeesCents,
    int? TransferAmountCents,
    DateTime? PaidAt,
    DateTime? RefundedAt
);
