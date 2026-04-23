CREATE OR REPLACE FUNCTION sp_update_user_password(
    p_user_id uuid,
    p_password_hash text
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM users WHERE "Id" = p_user_id) THEN
        RAISE EXCEPTION 'User not found';
    END IF;

    UPDATE users
    SET "PasswordHash" = p_password_hash,
        "EmailVerified" = true,
        "UpdatedAt" = now()
    WHERE "Id" = p_user_id;
END;
$$;
