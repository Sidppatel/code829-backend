CREATE OR REPLACE FUNCTION sp_signup_user(
    p_email text,
    p_email_hash text,
    p_first_name text,
    p_last_name text,
    p_password_hash text
) RETURNS SETOF users LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_id uuid;
BEGIN
    IF EXISTS (SELECT 1 FROM users WHERE "EmailHash" = p_email_hash) THEN
        RAISE EXCEPTION 'User with this email already exists';
    END IF;

    INSERT INTO users (
        "Email", "EmailHash", "FirstName", "LastName",
        "PasswordHash", "EmailVerified", "IsActive",
        "OptInLocationEmail", "HasCompletedOnboarding",
        "CreatedAt", "UpdatedAt"
    ) VALUES (
        p_email, p_email_hash, p_first_name, p_last_name,
        p_password_hash, false, true,
        false, false,
        now(), now()
    )
    RETURNING "Id" INTO v_id;

    RETURN QUERY SELECT * FROM users WHERE "Id" = v_id;
END;
$$;
