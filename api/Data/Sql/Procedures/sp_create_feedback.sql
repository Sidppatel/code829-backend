CREATE OR REPLACE FUNCTION sp_create_feedback(
    p_name text, p_email text, p_type text, p_message text, p_rating int,
    p_user_id uuid, p_user_agent text, p_ip text, p_diagnostics jsonb
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO feedbacks ("Id", "Name", "Email", "Type", "Message", "Rating",
        "UserId", "UserAgent", "IpAddress", "Diagnostics", "CreatedAt", "UpdatedAt")
    VALUES (gen_random_uuid(), p_name, p_email, p_type, p_message, p_rating,
        p_user_id, p_user_agent, p_ip, p_diagnostics, now(), now())
    RETURNING "Id" INTO v_id;
    RETURN v_id;
END; $$;
