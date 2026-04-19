CREATE OR REPLACE FUNCTION sp_create_admin_log(
    p_action text, p_actor_id uuid, p_actor_email text, p_actor_role text,
    p_entity_type text, p_entity_id uuid, p_description text,
    p_metadata_json text, p_ip text
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO admin_logs ("Id", "Timestamp", "Action", "ActorId", "ActorEmail", "ActorRole",
        "EntityType", "EntityId", "Description", "MetadataJson", "IpAddress")
    VALUES (gen_random_uuid(), now(), p_action, p_actor_id, p_actor_email, p_actor_role,
        p_entity_type, p_entity_id, p_description, p_metadata_json, p_ip)
    RETURNING "Id" INTO v_id;
    RETURN v_id;
END; $$;