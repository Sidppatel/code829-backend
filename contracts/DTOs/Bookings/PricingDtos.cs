namespace Contracts.DTOs.Bookings;

public record PricingQuoteRequest(
    Guid EventId,
    List<Guid>? TableIds = null,
    int? SeatCount = null,
    Guid? EventTicketTypeId = null
);

public record PricingQuoteDto(
    int SubtotalCents,
    int FeeCents,
    int TaxCents,
    int TotalCents,
    string Currency,
    string FormattedTotal,
    DateTime ExpiresAt
);
