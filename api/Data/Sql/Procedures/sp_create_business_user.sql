CREATE OR REPLACE FUNCTION sp_create_business_user(
    p_email text, p_email_hash text, p_first_name text, p_last_name text,
    p_password_hash text, p_role text
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO business_users ("Email", "EmailHash", "FirstName", "LastName",
        "PasswordHash", "Role", "IsActive", "FailedLoginAttempts", "CreatedAt", "UpdatedAt")
    VALUES (p_email, p_email_hash, p_first_name, p_last_name,
        p_password_hash, p_role, true, 0, now(), now())
    RETURNING "Id" INTO v_id;
    RETURN v_id;
END; $$;