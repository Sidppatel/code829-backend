namespace Contracts.DTOs.Purchases;

public record PricingQuoteRequest(
    Guid EventId,
    List<Guid>? TableIds = null,
    int? SeatCount = null,
    Guid? EventTicketTypeId = null
);

// Public — what guest sees while browsing / in cart / table modal.
// Single number. No subtotal, no fee, no tax.
public record PublicQuoteDto(
    int DisplayTotalCents,
    int SeatsIncluded,
    string Currency,
    string FormattedDisplayTotal,
    DateTime ExpiresAt
);

// Public — checkout screen only. Stripe Tax has run; tax appears for the first time.
public record CheckoutQuoteDto(
    int DisplayTotalCents,
    int TaxCents,
    int GrandTotalCents,
    int SeatsIncluded,
    string Currency,
    string FormattedDisplayTotal,
    string FormattedTax,
    string FormattedGrandTotal,
    string? TaxCalculationId,
    DateTime ExpiresAt
);

// Admin / Developer — full breakdown + per-line array.
public record AdminQuoteDto(
    int SubtotalCents,
    int FeeCents,
    int DisplayTotalCents,
    int TaxCents,
    int GrandTotalCents,
    int SeatsIncluded,
    string Currency,
    string FormattedDisplayTotal,
    string FormattedGrandTotal,
    string? TaxCalculationId,
    DateTime ExpiresAt,
    List<QuoteLineDto> Lines
);

public record QuoteLineDto(
    Guid? TableId,
    Guid? EventTicketTypeId,
    string Label,
    int Quantity,
    int UnitPriceCents,
    int LineFeeCents,
    int LineDisplayCents
);
