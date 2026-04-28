CREATE OR REPLACE FUNCTION sp_set_business_user_avatar_image(
    p_business_user_id uuid, p_image_id uuid
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_old_image_id uuid;
BEGIN
    SELECT "ImageId" INTO v_old_image_id FROM business_users WHERE "Id" = p_business_user_id;
    UPDATE business_users SET "ImageId" = p_image_id, "UpdatedAt" = now() WHERE "Id" = p_business_user_id;
    RETURN v_old_image_id;
END; $$;
