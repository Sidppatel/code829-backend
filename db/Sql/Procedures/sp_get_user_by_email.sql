CREATE OR REPLACE FUNCTION sp_get_user_by_email(p_email text)
RETURNS SETOF users
LANGUAGE sql STABLE AS $$
    SELECT * FROM users WHERE "Email" = p_email;
$$;