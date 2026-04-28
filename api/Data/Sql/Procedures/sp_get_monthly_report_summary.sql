CREATE OR REPLACE FUNCTION sp_get_monthly_report_summary(p_year int, p_month int)
RETURNS TABLE (
    "TotalPurchases"          int,
    "TotalChargedCents"       bigint,
    "TotalAdminPayoutsCents"  bigint,
    "TotalPlatformFeesCents"  bigint,
    "TotalStripeFeesCents"    bigint,
    "TotalTaxCollectedCents"  bigint,
    "NetPlatformRevenueCents" bigint
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
        SELECT p.*, st."TotalChargedCents", st."TransferAmountCents",
               st."StripeFeesCents", st."TaxAmountCents", st."PaidAt"
        FROM purchases p
        LEFT JOIN stripe_transactions st ON st."PurchaseId" = p."Id",
        window_bounds wb
        WHERE p."Status"::text IN ('Paid','CheckedIn')
          AND st."PaidAt" >= wb.from_ts
          AND st."PaidAt" <  wb.to_ts
    )
    SELECT
        COUNT(*)::int                                                                             AS "TotalPurchases",
        COALESCE(SUM(COALESCE("TotalChargedCents", "TotalCents"))::bigint, 0)                     AS "TotalChargedCents",
        COALESCE(SUM(COALESCE("TransferAmountCents", "SubtotalCents"))::bigint, 0)                AS "TotalAdminPayoutsCents",
        COALESCE(SUM("FeeCents")::bigint, 0)                                                      AS "TotalPlatformFeesCents",
        COALESCE(SUM(COALESCE("StripeFeesCents", 0))::bigint, 0)                                  AS "TotalStripeFeesCents",
        COALESCE(SUM(COALESCE("TaxAmountCents", 0))::bigint, 0)                                   AS "TotalTaxCollectedCents",
        (COALESCE(SUM("FeeCents")::bigint, 0) - COALESCE(SUM(COALESCE("StripeFeesCents", 0))::bigint, 0)) AS "NetPlatformRevenueCents"
    FROM src;
$$;
