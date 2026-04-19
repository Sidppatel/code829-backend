CREATE OR REPLACE FUNCTION sp_create_image(
    p_entity_type text, p_entity_id uuid, p_storage_key text, p_original_name text,
    p_size_bytes int, p_width int, p_height int,
    p_is_primary bool, p_sort_order int, p_uploaded_by uuid,
    p_uploader_type text DEFAULT NULL
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    IF p_is_primary THEN
        UPDATE images SET "IsPrimary" = false, "UpdatedAt" = now()
        WHERE "EntityType" = p_entity_type AND "EntityId" = p_entity_id AND "IsPrimary" = true;
    END IF;
    INSERT INTO images ("Id", "EntityType", "EntityId", "StorageKey", "OriginalName",
        "SizeBytes", "Width", "Height", "IsPrimary", "SortOrder", "UploadedById", "UploaderType",
        "CreatedAt", "UpdatedAt")
    VALUES (gen_random_uuid(), p_entity_type, p_entity_id, p_storage_key, p_original_name,
        p_size_bytes, p_width, p_height, p_is_primary, p_sort_order, p_uploaded_by, p_uploader_type,
        now(), now())
    RETURNING "Id" INTO v_id;
    RETURN v_id;
END; $$;