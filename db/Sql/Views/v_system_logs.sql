-- Reads system-actor entries from the unified audit_logs table. Legacy system_logs
-- table dropped in DropLegacyLogTables migration; shape preserved so existing
-- consumers (sp_get_system_logs, SystemLogView entity, DeveloperController) keep
-- compiling.
CREATE OR REPLACE VIEW v_system_logs AS
SELECT
    al."Id"                                                        AS "Id",
    al."CreatedAt"                                                 AS "Timestamp",
    COALESCE(al."MetadataJson" ->> 'category', 'EntityChange')     AS "Category",
    al."Action"                                                    AS "Action",
    al."MetadataJson" ->> 'source'                                 AS "Source",
    al."SubjectType"                                               AS "EntityType",
    al."SubjectId"                                                 AS "EntityId",
    al."MetadataJson" ->> 'before'                                 AS "BeforeJson",
    al."MetadataJson" ->> 'after'                                  AS "AfterJson",
    al."ActorId"                                                   AS "UserId",
    au."Email"                                                     AS "UserEmail",
    au."Role"                                                      AS "UserRole",
    al."CorrelationId"::text                                       AS "CorrelationId",
    NULLIF(al."MetadataJson" ->> 'duration_ms', '')::bigint        AS "DurationMs",
    al."MetadataJson"::text                                        AS "MetadataJson"
FROM audit_logs al
LEFT JOIN business_users au ON au."Id" = al."ActorId"
WHERE al."ActorType" = 'System';
