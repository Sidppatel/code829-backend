CREATE OR REPLACE FUNCTION sp_get_business_user_password_reset_token(p_token_hash text)
RETURNS TABLE(
    "TokenId" uuid,
    "BusinessUserId" uuid,
    "IsUsed" boolean,
    "ExpiresAt" timestamptz,
    "BusinessUserEmail" text
) LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT
        t."Id" AS "TokenId",
        t."BusinessUserId",
        t."IsUsed",
        t."ExpiresAt",
        a."Email"::text AS "BusinessUserEmail"
    FROM business_password_reset_tokens t
    LEFT JOIN business_users a ON a."Id" = t."BusinessUserId"
    WHERE t."TokenHash" = p_token_hash
    LIMIT 1;
$$;
