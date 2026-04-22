CREATE OR REPLACE FUNCTION sp_create_admin_log(
    p_action text, p_business_user_id uuid,
    p_entity_type text, p_entity_id uuid, p_description text,
    p_metadata_json text, p_ip text
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO business_logs ("Id", "Timestamp", "Action", "BusinessUserId",
        "EntityType", "EntityId", "Description", "MetadataJson", "IpAddress")
    VALUES (gen_random_uuid(), now(), p_action, p_business_user_id,
        p_entity_type, p_entity_id, p_description, p_metadata_json, p_ip)
    RETURNING "Id" INTO v_id;
    RETURN v_id;
END; $$;
