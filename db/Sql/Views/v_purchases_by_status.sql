CREATE OR REPLACE VIEW v_purchases_by_status AS
SELECT
    "Status"::text AS "Status",
    COUNT(*)::int AS "Count"
FROM purchases
GROUP BY "Status";
