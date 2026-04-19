CREATE OR REPLACE FUNCTION sp_get_admin_by_id(p_id uuid)
RETURNS SETOF admin_users
LANGUAGE sql STABLE AS $$
    SELECT * FROM admin_users WHERE "Id" = p_id;
$$;