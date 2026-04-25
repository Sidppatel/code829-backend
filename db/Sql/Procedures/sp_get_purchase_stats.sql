CREATE OR REPLACE FUNCTION sp_get_purchase_stats(
    p_business_user_ids uuid[],
    p_event_id uuid
)
RETURNS TABLE (
    "Total"     int,
    "Paid"      int,
    "CheckedIn" int,
    "Revenue"   bigint
)
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    SELECT
        COUNT(*)::int                                                                                  AS "Total",
        COUNT(*) FILTER (WHERE p."Status"::text IN ('Paid','CheckedIn'))::int                          AS "Paid",
        COUNT(*) FILTER (WHERE p."Status"::text = 'CheckedIn')::int                                    AS "CheckedIn",
        COALESCE(SUM(p."TotalCents") FILTER (WHERE p."Status"::text IN ('Paid','CheckedIn')), 0)::bigint AS "Revenue"
    FROM purchases p
    JOIN events e ON e."Id" = p."EventId"
    WHERE (p_business_user_ids IS NULL OR e."BusinessUserId" = ANY(p_business_user_ids))
      AND (p_event_id IS NULL OR p."EventId" = p_event_id);
$$;
