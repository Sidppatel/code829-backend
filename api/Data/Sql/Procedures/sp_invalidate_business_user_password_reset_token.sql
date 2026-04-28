CREATE OR REPLACE FUNCTION sp_invalidate_business_user_password_reset_token(p_token_hash text)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE business_password_reset_tokens
    SET "IsUsed" = true,
        "UsedAt" = now(),
        "UpdatedAt" = now()
    WHERE "TokenHash" = p_token_hash;
END;
$$;
