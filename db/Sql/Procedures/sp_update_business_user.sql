CREATE OR REPLACE FUNCTION sp_update_business_user(
    p_id uuid, p_first_name text DEFAULT NULL, p_last_name text DEFAULT NULL,
    p_phone text DEFAULT NULL, p_role text DEFAULT NULL,
    p_is_active boolean DEFAULT NULL, p_image_id uuid DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE business_users SET
        "FirstName" = COALESCE(p_first_name, "FirstName"),
        "LastName" = COALESCE(p_last_name, "LastName"),
        "Phone" = COALESCE(p_phone, "Phone"),
        "Role" = COALESCE(p_role, "Role"),
        "IsActive" = COALESCE(p_is_active, "IsActive"),
        "ImageId" = COALESCE(p_image_id, "ImageId"),
        "UpdatedAt" = now()
    WHERE "Id" = p_id;
END; $$;
