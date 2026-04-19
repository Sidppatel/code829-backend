CREATE OR REPLACE FUNCTION sp_get_admin_by_email(p_email text)
RETURNS SETOF admin_users
LANGUAGE sql STABLE AS $$
    SELECT * FROM admin_users WHERE "Email" = p_email;
$$;