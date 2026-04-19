CREATE OR REPLACE FUNCTION sp_clear_admin_user_avatar_image(
    p_admin_user_id uuid
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE
    v_old_image_id uuid;
BEGIN
    SELECT "AvatarImageId" INTO v_old_image_id FROM admin_users WHERE "Id" = p_admin_user_id;
    UPDATE admin_users SET "AvatarImageId" = NULL, "UpdatedAt" = now() WHERE "Id" = p_admin_user_id;
    RETURN v_old_image_id;
END; $$;
