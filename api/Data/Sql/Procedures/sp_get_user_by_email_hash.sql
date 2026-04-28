CREATE OR REPLACE FUNCTION sp_get_user_by_email_hash(p_email_hash text)
RETURNS SETOF users
LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT * FROM users WHERE "EmailHash" = p_email_hash;
$$;