CREATE OR REPLACE FUNCTION sp_create_magic_link(
    p_email text, p_token_hash text, p_expires_at timestamptz
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO magic_link_tokens ("Id", "TokenHash", "Email", "ExpiresAt", "IsUsed", "CreatedAt", "UpdatedAt")
    VALUES (gen_random_uuid(), p_token_hash, p_email, p_expires_at, false, now(), now())
    RETURNING "Id" INTO v_id;
    RETURN v_id;
END; $$;