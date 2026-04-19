CREATE OR REPLACE FUNCTION sp_admin_exists_by_email(p_email text)
RETURNS boolean
LANGUAGE sql STABLE AS $$
    SELECT EXISTS(SELECT 1 FROM admin_users WHERE "Email" = p_email);
$$;