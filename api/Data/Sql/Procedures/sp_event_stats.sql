CREATE OR REPLACE FUNCTION sp_event_stats(p_event_id uuid)
RETURNS TABLE(
    "TotalSold" int,
    "MaxCapacity" int,
    "FillRatePct" int,
    "GrossRevenueCents" bigint
) LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT
        COALESCE(v."TotalSold", 0)::int AS "TotalSold",
        COALESCE(v."TotalCapacity", 0)::int AS "MaxCapacity",
        CASE WHEN COALESCE(v."TotalCapacity", 0) > 0
            THEN ((COALESCE(v."TotalSold", 0)::numeric / v."TotalCapacity"::numeric) * 100)::int
            ELSE 0 END AS "FillRatePct",
        COALESCE((
            SELECT SUM(b."SubtotalCents")::bigint
            FROM purchases b
            WHERE b."EventId" = p_event_id
              AND b."Status" IN ('Paid', 'CheckedIn')
        ), 0) AS "GrossRevenueCents"
    FROM v_events v
    WHERE v."EventId" = p_event_id;
$$;