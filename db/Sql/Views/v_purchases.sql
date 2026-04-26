CREATE OR REPLACE VIEW v_purchases AS
SELECT
    b."Id" AS "PurchaseId", b."PurchaseNumber", b."Status"::text,
    b."SubtotalCents", b."FeeCents", b."TotalCents",
    b."QrToken", b."SeatsReserved", b."CreatedAt",
    b."UserId",
    u."Email" AS "UserEmail",
    u."FirstName" AS "UserFirstName",
    u."LastName" AS "UserLastName",
    b."EventId",
    e."Title" AS "EventTitle",
    e."Slug" AS "EventSlug",
    e."StartDate" AS "EventStartDate",
    e."EndDate" AS "EventEndDate",
    COALESCE(e."Category"::text, '') AS "EventCategory",
    e."ImagePath" AS "EventImagePath",
    v."Name" AS "VenueName",
    COALESCE(addr."Line1", '') AS "VenueAddress",
    COALESCE(addr."City", '') AS "VenueCity",
    COALESCE(addr."State", '') AS "VenueState",
    b."TableId",
    tbl."Label" AS "TableLabel",
    b."EventTicketTypeId",
    ett."Label" AS "EventTicketTypeLabel",
    st."Id" AS "StripeTransactionId",
    st."PaymentIntentId",
    st."TaxCalculationId",
    st."TaxTransactionId",
    st."Status"::text AS "PaymentStatus",
    st."AmountCents" AS "PaymentAmountCents",
    st."TotalChargedCents",
    st."TaxAmountCents",
    st."StripeFeesCents",
    st."TransferAmountCents",
    st."PaidAt", st."RefundedAt",
    COALESCE(tc.cnt, 0)::int AS "TicketCount",
    e."BusinessUserId",
    COALESCE(pt_labels.labels, ARRAY[]::text[]) AS "TableLabels"
FROM purchases b
JOIN users u ON b."UserId" = u."Id"
JOIN events e ON b."EventId" = e."Id"
JOIN venues v ON e."VenueId" = v."Id"
LEFT JOIN addresses addr ON v."AddressId" = addr."Id"
LEFT JOIN tables tbl ON b."TableId" = tbl."Id"
LEFT JOIN event_ticket_types ett ON b."EventTicketTypeId" = ett."Id"
LEFT JOIN stripe_transactions st ON st."PurchaseId" = b."Id"
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS cnt FROM purchase_tickets bt WHERE bt."PurchaseId" = b."Id"
) tc ON true
LEFT JOIN LATERAL (
    SELECT array_agg(t."Label" ORDER BY t."Label") AS labels
    FROM purchase_tables pt
    JOIN tables t ON t."Id" = pt."TableId"
    WHERE pt."PurchaseId" = b."Id"
) pt_labels ON true;