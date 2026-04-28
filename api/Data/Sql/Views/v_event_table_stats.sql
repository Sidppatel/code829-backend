CREATE OR REPLACE VIEW v_event_table_stats AS
SELECT
    "EventId",
    COALESCE(SUM("TotalTables"), 0)::int  AS "TotalTables",
    COALESCE(SUM("BookedTables"), 0)::int AS "BookedTables"
FROM v_event_tables_summary
WHERE "IsActive" = true
GROUP BY "EventId";
