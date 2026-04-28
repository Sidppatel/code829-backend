CREATE OR REPLACE FUNCTION sp_update_business_user_password(p_id uuid, p_password_hash text)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE business_users SET "PasswordHash" = p_password_hash, "UpdatedAt" = now() WHERE "Id" = p_id;
END; $$;