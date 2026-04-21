CREATE OR REPLACE VIEW v_system_logs AS
SELECT
    sl."Id",
    sl."Timestamp",
    sl."Category",
    sl."Action",
    sl."Source",
    sl."EntityType",
    sl."EntityId",
    sl."BeforeJson",
    sl."AfterJson",
    sl."UserId",
    au."Email" AS "UserEmail",
    au."Role"  AS "UserRole",
    sl."CorrelationId",
    sl."DurationMs",
    sl."MetadataJson"
FROM system_logs sl
LEFT JOIN business_users au ON au."Id" = sl."UserId";
