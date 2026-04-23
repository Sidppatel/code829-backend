CREATE OR REPLACE FUNCTION sp_upsert_user(
    p_email text, p_email_hash text, p_first_name text, p_last_name text
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    SELECT "Id" INTO v_id FROM users WHERE "Email" = p_email;
    IF v_id IS NULL THEN
        INSERT INTO users ("Id", "Email", "EmailHash", "FirstName", "LastName",
            "IsActive", "EmailVerified", "LastLoginAt", "OptInLocationEmail", "HasCompletedOnboarding",
            "CreatedAt", "UpdatedAt")
        VALUES (gen_random_uuid(), p_email, p_email_hash, p_first_name, p_last_name,
            true, true, now(), false, false, now(), now())
        RETURNING "Id" INTO v_id;
    ELSE
        UPDATE users SET "LastLoginAt" = now(), "EmailVerified" = true, "UpdatedAt" = now() WHERE "Id" = v_id;
    END IF;
    RETURN v_id;
END; $$;