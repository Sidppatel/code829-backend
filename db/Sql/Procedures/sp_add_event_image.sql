CREATE OR REPLACE FUNCTION sp_add_event_image(
    p_event_id uuid,
    p_storage_key text,
    p_original_name text,
    p_size_bytes int,
    p_width int,
    p_height int,
    p_uploaded_by uuid,
    p_uploader_type text DEFAULT NULL,
    p_alt_text text DEFAULT NULL,
    p_caption text DEFAULT NULL,
    p_content_type text DEFAULT NULL,
    p_checksum text DEFAULT NULL
) RETURNS TABLE(
    "ImageId" uuid,
    "EventImageId" uuid,
    "SortOrder" int,
    "IsPrimary" boolean
) LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_image_id uuid;
    v_event_image_id uuid;
    v_sort_order int;
    v_is_primary boolean;
    v_has_primary boolean;
BEGIN
    SELECT COALESCE(MAX(ei."SortOrder") + 1, 0) INTO v_sort_order
    FROM event_images ei WHERE ei."EventId" = p_event_id;

    SELECT EXISTS(SELECT 1 FROM event_images ei WHERE ei."EventId" = p_event_id AND ei."IsPrimary" = true)
    INTO v_has_primary;
    v_is_primary := NOT v_has_primary;

    INSERT INTO images ("Id", "EntityType", "EntityId", "StorageKey", "OriginalName",
        "SizeBytes", "Width", "Height", "SortOrder",
        "UploadedById", "UploaderType", "AltText", "Caption", "ContentType", "Checksum",
        "CreatedAt", "UpdatedAt")
    VALUES (gen_random_uuid(), 'event', p_event_id, p_storage_key, p_original_name,
        p_size_bytes, p_width, p_height, v_sort_order,
        p_uploaded_by, p_uploader_type, p_alt_text, p_caption, p_content_type, p_checksum,
        now(), now())
    RETURNING "Id" INTO v_image_id;

    INSERT INTO event_images ("Id", "EventId", "ImageId", "SortOrder", "IsPrimary",
        "CreatedAt", "UpdatedAt")
    VALUES (gen_random_uuid(), p_event_id, v_image_id, v_sort_order, v_is_primary,
        now(), now())
    RETURNING "Id" INTO v_event_image_id;

    RETURN QUERY SELECT v_image_id, v_event_image_id, v_sort_order, v_is_primary;
END; $$;
