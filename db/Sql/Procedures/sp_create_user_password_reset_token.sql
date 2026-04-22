CREATE OR REPLACE FUNCTION sp_create_user_password_reset_token(
    p_user_id uuid,
    p_token_hash text,
    p_expires_at timestamptz,
    p_ip_address text
) RETURNS SETOF user_password_reset_tokens LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_id uuid;
BEGIN
    INSERT INTO user_password_reset_tokens (
        "UserId", "TokenHash", "ExpiresAt", "IpAddress"
    ) VALUES (
        p_user_id, p_token_hash, p_expires_at, p_ip_address
    )
    RETURNING "Id" INTO v_id;

    RETURN QUERY SELECT * FROM user_password_reset_tokens WHERE "Id" = v_id;
END;
$$;
