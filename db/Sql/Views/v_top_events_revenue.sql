CREATE OR REPLACE VIEW v_top_events_revenue AS
SELECT
    e."Id" AS "EventId",
    e."Title" AS "Title",
    COUNT(p.*)::int AS "PurchaseCount",
    COALESCE(SUM(p."TotalCents")::bigint, 0) AS "RevenueCents"
FROM purchases p
JOIN events e ON e."Id" = p."EventId"
WHERE p."Status"::text IN ('Paid','CheckedIn')
GROUP BY e."Id", e."Title"
ORDER BY "RevenueCents" DESC
LIMIT 10;
