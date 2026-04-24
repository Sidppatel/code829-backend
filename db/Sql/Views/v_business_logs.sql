-- Reads admin-actor entries from the unified audit_logs table, joining business_users
-- for the email/role display columns. Legacy business_logs table dropped in
-- 20260424_DropLegacyLogTables migration; shape preserved so existing SP consumers
-- and C# BusinessLogView entity keep compiling.
CREATE OR REPLACE VIEW v_business_logs AS
SELECT
    al."Id"                                   AS "Id",
    al."CreatedAt"                            AS "Timestamp",
    al."Action"                               AS "Action",
    al."ActorId"                              AS "BusinessUserId",
    au."Email"                                AS "BusinessUserEmail",
    au."Role"                                 AS "BusinessUserRole",
    al."SubjectType"                          AS "EntityType",
    al."SubjectId"                            AS "EntityId",
    NULLIF(al."MetadataJson" ->> 'description', '') AS "Description",
    al."MetadataJson"::text                   AS "MetadataJson",
    al."Ip"                                   AS "IpAddress"
FROM audit_logs al
LEFT JOIN business_users au ON au."Id" = al."ActorId"
WHERE al."ActorType" = 'Admin';
