CREATE OR REPLACE FUNCTION sp_create_admin_password_reset_token(
    p_admin_user_id uuid,
    p_token_hash text,
    p_expires_at timestamptz,
    p_email text
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE
    v_id uuid;
BEGIN
    INSERT INTO admin_password_reset_tokens (
        "AdminUserId", "TokenHash", "ExpiresAt", "Email", "IsUsed"
    ) VALUES (
        p_admin_user_id, p_token_hash, p_expires_at, p_email, false
    )
    RETURNING "Id" INTO v_id;

    RETURN v_id;
END;
$$;
