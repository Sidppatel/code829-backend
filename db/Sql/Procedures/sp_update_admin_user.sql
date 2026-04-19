CREATE OR REPLACE FUNCTION sp_update_admin_user(
    p_id uuid, p_first_name text DEFAULT NULL, p_last_name text DEFAULT NULL,
    p_phone text DEFAULT NULL, p_role text DEFAULT NULL,
    p_is_active boolean DEFAULT NULL, p_avatar_image_id uuid DEFAULT NULL
) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE admin_users SET
        "FirstName" = COALESCE(p_first_name, "FirstName"),
        "LastName" = COALESCE(p_last_name, "LastName"),
        "Phone" = COALESCE(p_phone, "Phone"),
        "Role" = COALESCE(p_role, "Role"),
        "IsActive" = COALESCE(p_is_active, "IsActive"),
        "AvatarImageId" = COALESCE(p_avatar_image_id, "AvatarImageId"),
        "UpdatedAt" = now()
    WHERE "Id" = p_id;
END; $$;
