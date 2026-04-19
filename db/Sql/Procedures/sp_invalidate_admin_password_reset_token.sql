CREATE OR REPLACE FUNCTION sp_invalidate_admin_password_reset_token(p_token_hash text)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE admin_password_reset_tokens
    SET "IsUsed" = true,
        "UsedAt" = now(),
        "UpdatedAt" = now()
    WHERE "TokenHash" = p_token_hash;
END;
$$;
