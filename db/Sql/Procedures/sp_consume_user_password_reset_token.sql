CREATE OR REPLACE FUNCTION sp_consume_user_password_reset_token(
    p_token_hash text
) RETURNS SETOF users LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_user_id uuid;
BEGIN
    SELECT "UserId" INTO v_user_id
    FROM user_password_reset_tokens
    WHERE "TokenHash" = p_token_hash
      AND "UsedAt" IS NULL
      AND "ExpiresAt" > now()
    LIMIT 1;

    IF v_user_id IS NULL THEN
        RAISE EXCEPTION 'Invalid or expired token';
    END IF;

    UPDATE user_password_reset_tokens
    SET "UsedAt" = now(),
        "UpdatedAt" = now()
    WHERE "TokenHash" = p_token_hash;

    RETURN QUERY SELECT * FROM users WHERE "Id" = v_user_id;
END;
$$;
