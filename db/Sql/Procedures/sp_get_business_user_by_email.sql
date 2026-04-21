CREATE OR REPLACE FUNCTION sp_get_business_user_by_email(p_email text)
RETURNS SETOF business_users
LANGUAGE sql STABLE AS $$
    SELECT * FROM business_users WHERE "Email" = p_email;
$$;