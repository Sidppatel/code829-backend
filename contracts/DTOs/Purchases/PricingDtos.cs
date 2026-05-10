namespace Contracts.DTOs.Purchases;

public record PricingQuoteRequest(
    Guid EventId,
    List<Guid>? TableIds = null,
    int? SeatCount = null,
    Guid? EventTicketTypeId = null
);

public record PublicQuoteDto(
    int DisplayTotalCents,
    int SeatsIncluded,
    string Currency,
    string FormattedDisplayTotal,
    DateTime ExpiresAt
);

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
