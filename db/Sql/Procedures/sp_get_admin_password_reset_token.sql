CREATE OR REPLACE FUNCTION sp_get_admin_password_reset_token(p_token_hash text)
RETURNS TABLE(
    "TokenId" uuid,
    "AdminUserId" uuid,
    "IsUsed" boolean,
    "ExpiresAt" timestamptz,
    "AdminEmail" text
) LANGUAGE sql STABLE AS $$
    SELECT
        t."Id" AS "TokenId",
        t."AdminUserId",
        t."IsUsed",
        t."ExpiresAt",
        a."Email"::text AS "AdminEmail"
    FROM admin_password_reset_tokens t
    LEFT JOIN admin_users a ON a."Id" = t."AdminUserId"
    WHERE t."TokenHash" = p_token_hash
    LIMIT 1;
$$;
