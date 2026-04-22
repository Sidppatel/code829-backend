CREATE OR REPLACE FUNCTION sp_create_system_log(
    p_category text, p_action text, p_source text,
    p_entity_type text, p_entity_id uuid,
    p_before_json text, p_after_json text,
    p_user_id uuid, p_correlation_id text, p_duration_ms bigint, p_metadata_json text
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO system_logs ("Id", "Timestamp", "Category", "Action", "Source",
        "EntityType", "EntityId", "BeforeJson", "AfterJson", "UserId",
        "CorrelationId", "DurationMs", "MetadataJson")
    VALUES (gen_random_uuid(), now(), p_category, p_action, p_source,
        p_entity_type, p_entity_id, p_before_json, p_after_json, p_user_id,
        p_correlation_id, p_duration_ms, p_metadata_json)
    RETURNING "Id" INTO v_id;
    RETURN v_id;
END; $$;
