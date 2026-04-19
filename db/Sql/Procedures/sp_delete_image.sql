CREATE OR REPLACE FUNCTION sp_delete_image(p_image_id uuid) RETURNS text LANGUAGE plpgsql AS $$
DECLARE v_key text;
BEGIN
    DELETE FROM images WHERE "Id" = p_image_id RETURNING "StorageKey" INTO v_key;
    RETURN v_key;
END; $$;