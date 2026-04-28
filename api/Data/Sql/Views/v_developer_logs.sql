-- Reads developer-actor (exception) entries from the unified audit_logs table. Shape
-- mirrors the old developer_logs table so DeveloperController + DeveloperLogDto keep
-- their contract. ErrorHandlingMiddleware writes exceptions with actor_type='System'
-- and event_type='Exception', packing severity/message/stack/path/method/status into
-- the metadata JSON; both paths are included via the UNION-style WHERE clause.
CREATE OR REPLACE VIEW v_developer_logs AS
SELECT
    al."Id"                                                        AS "Id",
    al."CreatedAt"                                                 AS "Timestamp",
    COALESCE(al."MetadataJson" ->> 'severity', 'Error')            AS "Severity",
    COALESCE(al."MetadataJson" ->> 'message', al."Action")         AS "Message",
    al."MetadataJson" ->> 'exception_type'                         AS "ExceptionType",
    al."MetadataJson" ->> 'stack_trace'                            AS "StackTrace",
    al."MetadataJson" ->> 'request_path'                           AS "RequestPath",
    al."MetadataJson" ->> 'request_method'                         AS "RequestMethod",
    NULLIF(al."MetadataJson" ->> 'status_code', '')::int           AS "StatusCode",
    al."ActorId"                                                   AS "BusinessUserId",
    al."Ip"                                                        AS "IpAddress",
    al."CorrelationId"::text                                       AS "CorrelationId",
    al."MetadataJson"::text                                        AS "MetadataJson"
FROM audit_logs al
WHERE al."ActorType" = 'Developer'
   OR (al."ActorType" = 'System' AND al."EventType" = 'Exception');
