-- Backfill audit_logs from legacy business_logs / developer_logs / system_logs.
-- Idempotent-safe when run on an empty audit_logs table; re-running will duplicate rows
-- (migrations run once per schema — guarded by __EFMigrationsHistory).
--
-- Column width enforcement: audit_logs.EventType + audit_logs.Action are
-- character varying(128). Legacy columns that can exceed that width are LEFT()-
-- truncated at the projection. Full values are retained in the metadata JSON so
-- no data is lost.
--   developer_logs.ExceptionType (512) → EventType (128): LEFT + full in metadata
--   developer_logs.Message (4096)       → Action (128):    LEFT + full in metadata
--   developer_logs.StackTrace (text)    → preserved in metadata only

INSERT INTO audit_logs (
    "Id", "CreatedAt", "EventType", "ActorType", "ActorId",
    "SubjectType", "SubjectId", "Action", "MetadataJson", "Ip", "CorrelationId"
)
SELECT
    gen_random_uuid(),
    "Timestamp",
    LEFT("Action", 128),
    'Admin',
    "BusinessUserId",
    "EntityType",
    "EntityId",
    LEFT("Action", 128),
    CASE
        WHEN "MetadataJson" IS NULL THEN NULL
        WHEN "MetadataJson" ~ '^\s*[{\[]' THEN "MetadataJson"::jsonb
        ELSE jsonb_build_object('raw', "MetadataJson")
    END,
    NULLIF("IpAddress", ''),
    NULL::uuid
FROM business_logs

UNION ALL

SELECT
    gen_random_uuid(),
    "Timestamp",
    LEFT(COALESCE("ExceptionType", 'developer.log'), 128),
    'Developer',
    "BusinessUserId",
    NULL,
    NULL,
    LEFT("Message", 128),
    jsonb_strip_nulls(jsonb_build_object(
        'severity', "Severity",
        'full_message', "Message",
        'exception_type', "ExceptionType",
        'stack_trace', "StackTrace",
        'request_path', "RequestPath",
        'request_method', "RequestMethod",
        'status_code', "StatusCode",
        'legacy_metadata', CASE
            WHEN "MetadataJson" IS NULL THEN NULL
            WHEN "MetadataJson" ~ '^\s*[{\[]' THEN "MetadataJson"::jsonb
            ELSE jsonb_build_object('raw', "MetadataJson")
        END
    )),
    NULLIF("IpAddress", ''),
    NULL::uuid
FROM developer_logs

UNION ALL

SELECT
    gen_random_uuid(),
    "Timestamp",
    LEFT("Category"::text, 128),
    'System',
    "UserId",
    "EntityType",
    "EntityId",
    LEFT("Action", 128),
    CASE
        WHEN "MetadataJson" IS NULL THEN NULL
        WHEN "MetadataJson" ~ '^\s*[{\[]' THEN "MetadataJson"::jsonb
        ELSE jsonb_build_object('raw', "MetadataJson")
    END,
    NULL,
    NULL::uuid
FROM system_logs;
