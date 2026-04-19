CREATE OR REPLACE FUNCTION sp_consume_magic_link(p_token_hash text)
RETURNS TABLE (
    "Id" uuid, "Email" text, "ExpiresAt" timestamptz
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    UPDATE magic_link_tokens AS t
    SET "IsUsed" = true, "UsedAt" = now(), "UpdatedAt" = now()
    WHERE t."TokenHash" = p_token_hash AND t."IsUsed" = false AND t."ExpiresAt" > now()
    RETURNING t."Id", t."Email"::text, t."ExpiresAt";
END; $$;