CREATE OR REPLACE FUNCTION sp_update_user_profile(
    p_user_id uuid, p_first_name text, p_last_name text, p_phone text,
    p_address text, p_city text, p_state text, p_zip text, p_opt_in bool
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_address_id uuid;
BEGIN
    SELECT "AddressId" INTO v_address_id FROM users WHERE "Id" = p_user_id;
    IF v_address_id IS NULL AND (p_address IS NOT NULL OR p_city IS NOT NULL) THEN
        INSERT INTO addresses ("Id", "Line1", "City", "State", "ZipCode", "CreatedAt", "UpdatedAt")
        VALUES (gen_random_uuid(), COALESCE(p_address,''), COALESCE(p_city,''),
            COALESCE(p_state,''), COALESCE(p_zip,''), now(), now())
        RETURNING "Id" INTO v_address_id;
        UPDATE users SET "AddressId" = v_address_id WHERE "Id" = p_user_id;
    ELSIF v_address_id IS NOT NULL THEN
        UPDATE addresses SET
            "Line1" = COALESCE(p_address, "Line1"),
            "City" = COALESCE(p_city, "City"),
            "State" = COALESCE(p_state, "State"),
            "ZipCode" = COALESCE(p_zip, "ZipCode"),
            "UpdatedAt" = now()
        WHERE "Id" = v_address_id;
    END IF;
    UPDATE users SET
        "FirstName" = COALESCE(p_first_name, "FirstName"),
        "LastName" = COALESCE(p_last_name, "LastName"),
        "Phone" = p_phone,
        "OptInLocationEmail" = COALESCE(p_opt_in, "OptInLocationEmail"),
        "HasCompletedOnboarding" = true,
        "UpdatedAt" = now()
    WHERE "Id" = p_user_id;
END; $$;