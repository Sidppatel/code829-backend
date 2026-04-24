-- Backfill audit_logs from legacy business_logs / developer_logs / system_logs.
-- Idempotent-safe when run on an empty audit_logs table; re-running will duplicate rows
-- (migrations run once per schema — guarded by __EFMigrationsHistory).

INSERT INTO audit_logs (
    "Id", "CreatedAt", "EventType", "ActorType", "ActorId",
    "SubjectType", "SubjectId", "Action", "MetadataJson", "Ip", "CorrelationId"
)
SELECT
    gen_random_uuid(),
    "Timestamp",
    "Action",
    'Admin',
    "BusinessUserId",
    "EntityType",
    "EntityId",
    "Action",
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
    COALESCE("ExceptionType", 'developer.log'),
    'Developer',
    "BusinessUserId",
    NULL,
    NULL,
    "Message",
    CASE
        WHEN "MetadataJson" IS NULL THEN NULL
        WHEN "MetadataJson" ~ '^\s*[{\[]' THEN "MetadataJson"::jsonb
        ELSE jsonb_build_object('raw', "MetadataJson")
    END,
    NULLIF("IpAddress", ''),
    NULL::uuid
FROM developer_logs

UNION ALL

SELECT
    gen_random_uuid(),
    "Timestamp",
    "Category"::text,
    'System',
    "UserId",
    "EntityType",
    "EntityId",
    "Action",
    CASE
        WHEN "MetadataJson" IS NULL THEN NULL
        WHEN "MetadataJson" ~ '^\s*[{\[]' THEN "MetadataJson"::jsonb
        ELSE jsonb_build_object('raw', "MetadataJson")
    END,
    NULL,
    NULL::uuid
FROM system_logs;
