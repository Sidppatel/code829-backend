CREATE OR REPLACE FUNCTION sp_create_business_user_password_reset_token(
    p_business_user_id uuid,
    p_token_hash text,
    p_expires_at timestamptz,
    p_email text
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_id uuid;
BEGIN
    INSERT INTO business_password_reset_tokens (
        "BusinessUserId", "TokenHash", "ExpiresAt", "Email", "IsUsed"
    ) VALUES (
        p_business_user_id, p_token_hash, p_expires_at, p_email, false
    )
    RETURNING "Id" INTO v_id;

    RETURN v_id;
END;
$$;
