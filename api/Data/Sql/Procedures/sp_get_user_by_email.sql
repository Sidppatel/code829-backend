CREATE OR REPLACE FUNCTION sp_get_user_by_email(p_email text)
RETURNS SETOF users
LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT * FROM users WHERE "Email" = p_email;
$$;