CREATE OR REPLACE FUNCTION sp_get_purchase_info_for_event(p_event_id uuid)
RETURNS TABLE (
    "TableId"       uuid,
    "PurchaseCount" int,
    "SeatsBooked"   int,
    "SubtotalCents" bigint
)
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    SELECT
        p."TableId"                                        AS "TableId",
        COUNT(*)::int                                      AS "PurchaseCount",
        COALESCE(SUM(p."SeatsReserved"), 0)::int           AS "SeatsBooked",
        COALESCE(SUM(p."SubtotalCents")::bigint, 0)        AS "SubtotalCents"
    FROM purchases p
    WHERE p."EventId" = p_event_id
      AND p."TableId" IS NOT NULL
      AND p."Status"::text IN ('Paid','CheckedIn')
    GROUP BY p."TableId";
$$;
