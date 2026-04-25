CREATE OR REPLACE FUNCTION sp_get_next_event_dashboard(p_now timestamptz)
RETURNS TABLE (
    "EventId"               uuid,
    "Title"                 text,
    "Slug"                  text,
    "Status"                text,
    "Category"              text,
    "StartDate"             timestamptz,
    "EndDate"               timestamptz,
    "VenueName"             text,
    "VenueAddress"          text,
    "VenueCity"             text,
    "VenueState"            text,
    "ImagePath"             text,
    "LayoutMode"            text,
    "DaysUntil"             int,
    "TotalPurchases"        int,
    "PaidPurchases"         int,
    "CheckedInPurchases"    int,
    "PendingPurchases"      int,
    "CancelledPurchases"    int,
    "RefundedPurchases"     int,
    "RevenueCents"          bigint,
    "PotentialRevenueCents" bigint,
    "TotalCapacity"         int,
    "SoldCount"             int
)
LANGUAGE plpgsql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_event_id uuid;
BEGIN
    SELECT e."Id" INTO v_event_id
    FROM events e
    WHERE e."Status"::text = 'Published' AND e."StartDate" > p_now
    ORDER BY e."StartDate"
    LIMIT 1;

    IF v_event_id IS NULL THEN
        SELECT e."Id" INTO v_event_id
        FROM events e
        WHERE e."StartDate" > p_now AND e."Status"::text != 'Cancelled'
        ORDER BY e."StartDate"
        LIMIT 1;
    END IF;

    IF v_event_id IS NULL THEN
        RETURN;
    END IF;

    RETURN QUERY
    SELECT
        e."Id"                                          AS "EventId",
        e."Title"::text                                 AS "Title",
        e."Slug"::text                                  AS "Slug",
        e."Status"::text                                AS "Status",
        COALESCE(e."Category"::text, '')                AS "Category",
        e."StartDate"                                   AS "StartDate",
        e."EndDate"                                     AS "EndDate",
        v."Name"::text                                  AS "VenueName",
        COALESCE(addr."Line1", '')::text                AS "VenueAddress",
        COALESCE(addr."City", '')::text                 AS "VenueCity",
        COALESCE(addr."State", '')::text                AS "VenueState",
        e."ImagePath"::text                             AS "ImagePath",
        e."LayoutMode"::text                            AS "LayoutMode",
        CEIL(EXTRACT(EPOCH FROM (e."StartDate" - p_now)) / 86400.0)::int AS "DaysUntil",
        ps.total_count                                  AS "TotalPurchases",
        ps.paid_count                                   AS "PaidPurchases",
        ps.checkin_count                                AS "CheckedInPurchases",
        ps.pending_count                                AS "PendingPurchases",
        ps.cancelled_count                              AS "CancelledPurchases",
        ps.refunded_count                               AS "RefundedPurchases",
        ps.revenue                                      AS "RevenueCents",
        (CASE
            WHEN e."LayoutMode"::text = 'Open' AND ettp.min_price IS NOT NULL
                THEN ettp.min_price::bigint * COALESCE(e."MaxCapacity", ts.total_capacity)::bigint
            ELSE ts.total_price::bigint
        END)::bigint                                    AS "PotentialRevenueCents",
        COALESCE(e."MaxCapacity", ts.total_capacity)    AS "TotalCapacity",
        (ps.paid_count + ps.checkin_count)              AS "SoldCount"
    FROM events e
    JOIN venues v ON v."Id" = e."VenueId"
    LEFT JOIN addresses addr ON v."AddressId" = addr."Id"
    CROSS JOIN LATERAL (
        SELECT
            COUNT(*)::int                                                                                  AS total_count,
            COUNT(*) FILTER (WHERE p."Status"::text = 'Paid')::int                                         AS paid_count,
            COUNT(*) FILTER (WHERE p."Status"::text = 'CheckedIn')::int                                    AS checkin_count,
            COUNT(*) FILTER (WHERE p."Status"::text = 'Pending')::int                                      AS pending_count,
            COUNT(*) FILTER (WHERE p."Status"::text = 'Cancelled')::int                                    AS cancelled_count,
            COUNT(*) FILTER (WHERE p."Status"::text = 'Refunded')::int                                     AS refunded_count,
            COALESCE(SUM(p."TotalCents") FILTER (WHERE p."Status"::text IN ('Paid','CheckedIn')), 0)::bigint AS revenue
        FROM purchases p
        WHERE p."EventId" = e."Id"
    ) ps
    CROSS JOIN LATERAL (
        SELECT
            COALESCE(SUM(et."Capacity"), 0)::int      AS total_capacity,
            COALESCE(SUM(et."PriceCents"::bigint), 0) AS total_price
        FROM tables t
        JOIN event_tables et ON et."Id" = t."EventTableId"
        WHERE t."EventId" = e."Id" AND t."IsActive" = true
    ) ts
    LEFT JOIN LATERAL (
        SELECT MIN(ett."PriceCents") AS min_price
        FROM event_ticket_types ett
        WHERE ett."EventId" = e."Id" AND ett."IsActive" = true
    ) ettp ON true
    WHERE e."Id" = v_event_id;
END;
$$;
