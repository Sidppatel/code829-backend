CREATE OR REPLACE FUNCTION sp_create_audit_log(
    p_event_type text,
    p_actor_type text,
    p_actor_id uuid,
    p_subject_type text,
    p_subject_id uuid,
    p_action text,
    p_metadata_json text,
    p_ip text,
    p_correlation_id uuid
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    IF p_actor_type NOT IN ('User','Admin','Developer','System') THEN
        RAISE EXCEPTION 'invalid actor_type: %', p_actor_type;
    END IF;

    INSERT INTO audit_logs (
        "Id", "CreatedAt", "EventType", "ActorType", "ActorId",
        "SubjectType", "SubjectId", "Action", "MetadataJson", "Ip", "CorrelationId"
    )
    VALUES (
        gen_random_uuid(), now(), p_event_type, p_actor_type, p_actor_id,
        p_subject_type, p_subject_id, p_action,
        CASE WHEN p_metadata_json IS NULL THEN NULL ELSE p_metadata_json::jsonb END,
        p_ip,
        p_correlation_id
    )
    RETURNING "Id" INTO v_id;
    RETURN v_id;
END; $$;
