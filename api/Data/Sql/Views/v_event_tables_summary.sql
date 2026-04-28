CREATE OR REPLACE VIEW v_event_tables_summary AS
SELECT
    et."Id" AS "EventTableId", et."EventId", et."Label", et."Capacity",
    et."Shape"::text, et."Color", et."PriceCents", et."PlatformFeeCents",
    et."IsActive",
    COALESCE(ts.total, 0)::int AS "TotalTables",
    COALESCE(ts.available, 0)::int AS "AvailableTables",
    COALESCE(ts.locked, 0)::int AS "LockedTables",
    COALESCE(ts.booked, 0)::int AS "BookedTables"
FROM event_tables et
LEFT JOIN LATERAL (
    SELECT
        COUNT(*)::int AS total,
        COUNT(*) FILTER (WHERE t."Status" = 'Available' AND t."IsActive")::int AS available,
        COUNT(*) FILTER (WHERE t."Status" = 'Locked')::int AS locked,
        COUNT(*) FILTER (WHERE t."Status" = 'Booked')::int AS booked
    FROM tables t WHERE t."EventTableId" = et."Id"
) ts ON true;