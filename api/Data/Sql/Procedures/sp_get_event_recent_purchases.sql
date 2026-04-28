CREATE OR REPLACE FUNCTION sp_get_event_recent_purchases(p_event_id uuid, p_limit int)
RETURNS TABLE (
    "PurchaseId"     uuid,
    "PurchaseNumber" varchar,
    "UserName"       text,
    "UserEmail"      varchar,
    "Status"         text,
    "TotalCents"     int,
    "CreatedAt"      timestamptz
)
LANGUAGE sql STABLE
SET search_path = public, extensions, pg_catalog
AS $$
    SELECT
        p."Id"             AS "PurchaseId",
        p."PurchaseNumber" AS "PurchaseNumber",
        (u."FirstName" || ' ' || u."LastName") AS "UserName",
        u."Email"          AS "UserEmail",
        p."Status"::text   AS "Status",
        p."TotalCents"     AS "TotalCents",
        p."CreatedAt"      AS "CreatedAt"
    FROM purchases p
    JOIN users u ON u."Id" = p."UserId"
    WHERE p."EventId" = p_event_id
    ORDER BY p."CreatedAt" DESC
    LIMIT p_limit;
$$;
