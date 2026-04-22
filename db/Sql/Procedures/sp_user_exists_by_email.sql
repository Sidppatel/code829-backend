CREATE OR REPLACE FUNCTION sp_user_exists_by_email(p_email text)
RETURNS boolean
LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT EXISTS(SELECT 1 FROM users WHERE "Email" = p_email);
$$;