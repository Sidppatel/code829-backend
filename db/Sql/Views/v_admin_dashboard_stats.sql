CREATE OR REPLACE VIEW v_admin_dashboard_stats AS
SELECT
    (SELECT COUNT(*)::int FROM events) AS "TotalEvents",
    (SELECT COUNT(*)::int FROM events WHERE "Status"::text = 'Published') AS "PublishedEvents",
    (SELECT COUNT(*)::int FROM purchases) AS "TotalPurchases",
    (SELECT COUNT(*)::int FROM purchases WHERE "Status"::text = 'Paid') AS "PaidPurchases",
    (SELECT COUNT(*)::int FROM purchases WHERE "Status"::text = 'CheckedIn') AS "CheckedInPurchases",
    COALESCE(
        (SELECT SUM("TotalCents")::bigint FROM purchases WHERE "Status"::text IN ('Paid','CheckedIn')),
        0
    ) AS "TotalRevenueCents",
    (SELECT COUNT(*)::int FROM users) AS "TotalUsers",
    (SELECT COUNT(*)::int FROM venues) AS "TotalVenues";
