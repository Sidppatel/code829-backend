CREATE OR REPLACE VIEW v_event_ticket_types_summary AS
SELECT
    ett."Id" AS "EventTicketTypeId", ett."EventId", ett."Label", ett."PriceCents",
    ett."PlatformFeeCents", ett."MaxQuantity", ett."SortOrder", ett."IsActive",
    ett."Description",
    ett."PriceCents" + COALESCE(ett."PlatformFeeCents", 0) AS "TotalPriceCents",
    COALESCE(bs.sold, 0)::int AS "SoldCount",
    CASE
        WHEN ett."MaxQuantity" IS NULL THEN -1
        ELSE GREATEST(0, ett."MaxQuantity" - COALESCE(bs.sold, 0))
    END::int AS "AvailableCount"
FROM event_ticket_types ett
LEFT JOIN LATERAL (
    SELECT COALESCE(SUM(b."SeatsReserved"), 0)::int AS sold
    FROM purchases b
    WHERE b."EventTicketTypeId" = ett."Id"
      AND b."Status" IN ('Pending', 'Paid', 'CheckedIn')
) bs ON true;