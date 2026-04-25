CREATE OR REPLACE VIEW v_events_by_category AS
SELECT
    "Category"::text AS "Category",
    COUNT(*)::int AS "Count"
FROM events
GROUP BY "Category";
