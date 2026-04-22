CREATE OR REPLACE FUNCTION sp_clear_user_image(
    p_user_id uuid
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_old_image_id uuid;
BEGIN
    SELECT "ImageId" INTO v_old_image_id FROM users WHERE "Id" = p_user_id;
    UPDATE users SET "ImageId" = NULL, "UpdatedAt" = now() WHERE "Id" = p_user_id;
    RETURN v_old_image_id;
END; $$;
