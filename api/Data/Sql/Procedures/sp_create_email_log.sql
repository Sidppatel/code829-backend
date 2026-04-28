CREATE OR REPLACE FUNCTION sp_create_email_log(
    p_recipient text, p_subject text, p_body text, p_status text
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO email_logs ("Id", "Timestamp", "Recipient", "Subject", "Body", "Status")
    VALUES (gen_random_uuid(), now(), p_recipient, p_subject, p_body, p_status)
    RETURNING "Id" INTO v_id;
    RETURN v_id;
END; $$;