CREATE OR REPLACE VIEW v_tables AS
SELECT
    t."Id" AS "TableId", t."EventId", t."EventTableId",
    t."Label", t."GridRow", t."GridCol",
    t."IsActive", t."SortOrder",
    t."Status"::text,
    t."LockedByUserId", t."LockExpiresAt",
    t."CreatedAt", t."UpdatedAt",
    et."Capacity", et."Shape"::text, et."Color",
    et."PriceCents", et."PlatformFeeCents",
    et."PriceCents" + COALESCE(et."PlatformFeeCents", 0) AS "TotalPriceCents",
    et."Label" AS "EventTableLabel"
FROM tables t
JOIN event_tables et ON t."EventTableId" = et."Id";