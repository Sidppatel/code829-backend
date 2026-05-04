CREATE OR REPLACE FUNCTION sp_update_business_user_last_login(p_id uuid)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE business_users
       SET "LastLoginAt" = now(),
           "LastRequestAt" = now(),
           "UpdatedAt" = now()
     WHERE "Id" = p_id;
END; $$;
