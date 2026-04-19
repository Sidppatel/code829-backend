CREATE OR REPLACE FUNCTION sp_get_user_by_id(p_user_id uuid)
RETURNS SETOF users
LANGUAGE sql STABLE AS $$
    SELECT * FROM users WHERE "Id" = p_user_id;
$$;