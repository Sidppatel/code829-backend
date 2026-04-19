CREATE OR REPLACE FUNCTION sp_consume_magic_link(p_token_hash text)
RETURNS TABLE (
    "Id" uuid, "Email" text, "ExpiresAt" timestamptz
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    UPDATE magic_link_tokens
    SET "IsUsed" = true, "UsedAt" = now(), "UpdatedAt" = now()
    WHERE "TokenHash" = p_token_hash AND "IsUsed" = false AND "ExpiresAt" > now()
    RETURNING magic_link_tokens."Id", magic_link_tokens."Email"::text, magic_link_tokens."ExpiresAt";
END; $$;