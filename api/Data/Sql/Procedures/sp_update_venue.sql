CREATE OR REPLACE FUNCTION sp_update_venue(
    p_id uuid, p_name text, p_description text, p_image_path text,
    p_phone text, p_email text, p_website text, p_is_active bool,
    p_line1 text, p_city text, p_state text, p_zip text
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_addr_id uuid;
BEGIN
    SELECT "AddressId" INTO v_addr_id FROM venues WHERE "Id" = p_id;
    IF v_addr_id IS NOT NULL THEN
        UPDATE addresses SET
            "Line1" = COALESCE(p_line1, "Line1"),
            "City" = COALESCE(p_city, "City"),
            "State" = COALESCE(p_state, "State"),
            "ZipCode" = COALESCE(p_zip, "ZipCode"),
            "UpdatedAt" = now()
        WHERE "Id" = v_addr_id;
    END IF;
    UPDATE venues SET
        "Name" = COALESCE(p_name, "Name"),
        "Description" = COALESCE(p_description, "Description"),
        "ImagePath" = COALESCE(p_image_path, "ImagePath"),
        "Phone" = p_phone, "Email" = p_email, "Website" = p_website,
        "IsActive" = COALESCE(p_is_active, "IsActive"),
        "UpdatedAt" = now()
    WHERE "Id" = p_id;
END; $$;