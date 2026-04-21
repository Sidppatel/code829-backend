CREATE OR REPLACE FUNCTION sp_get_business_user_by_id(p_id uuid)
RETURNS SETOF business_users
LANGUAGE sql STABLE AS $$
    SELECT * FROM business_users WHERE "Id" = p_id;
$$;