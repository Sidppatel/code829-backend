CREATE OR REPLACE FUNCTION sp_get_monthly_report_by_event(p_year int, p_month int)
RETURNS TABLE (
    "EventId"          uuid,
    "EventTitle"       varchar,
    "PurchaseCount"    int,
    "ChargedCents"     bigint,
    "AdminPayoutCents" bigint,
    "PlatformFeeCents" bigint,
    "StripeFeesCents"  bigint,
    "TaxCollectedCents" bigint
)
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    WITH window_bounds AS (
        SELECT
            make_timestamptz(p_year, p_month, 1, 0, 0, 0, 'UTC') AS from_ts,
            (make_timestamptz(p_year, p_month, 1, 0, 0, 0, 'UTC') + interval '1 month') AS to_ts
    ),
    src AS (
        SELECT
            p."EventId",
            e."Title" AS event_title,
            p."TotalCents", p."SubtotalCents", p."FeeCents",
            st."TotalChargedCents", st."TransferAmountCents",
            st."StripeFeesCents", st."TaxAmountCents", st."PaidAt"
        FROM purchases p
        JOIN events e ON e."Id" = p."EventId"
        LEFT JOIN stripe_transactions st ON st."PurchaseId" = p."Id",
        window_bounds wb
        WHERE p."Status"::text IN ('Paid','CheckedIn')
          AND st."PaidAt" >= wb.from_ts
          AND st."PaidAt" <  wb.to_ts
    )
    SELECT
        "EventId"                                                                  AS "EventId",
        event_title::varchar                                                       AS "EventTitle",
        COUNT(*)::int                                                              AS "PurchaseCount",
        COALESCE(SUM(COALESCE("TotalChargedCents", "TotalCents"))::bigint, 0)      AS "ChargedCents",
        COALESCE(SUM(COALESCE("TransferAmountCents", "SubtotalCents"))::bigint, 0) AS "AdminPayoutCents",
        COALESCE(SUM("FeeCents")::bigint, 0)                                       AS "PlatformFeeCents",
        COALESCE(SUM(COALESCE("StripeFeesCents", 0))::bigint, 0)                   AS "StripeFeesCents",
        COALESCE(SUM(COALESCE("TaxAmountCents", 0))::bigint, 0)                    AS "TaxCollectedCents"
    FROM src
    GROUP BY "EventId", event_title
    ORDER BY "ChargedCents" DESC;
$$;
