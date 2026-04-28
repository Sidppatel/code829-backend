CREATE OR REPLACE FUNCTION sp_create_invitation(
    p_email text, p_token_hash text, p_role text,
    p_invited_by uuid, p_expires_at timestamptz
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO invitations ("Id", "Email", "TokenHash", "Role",
        "InvitedByBusinessUserId", "Status", "ExpiresAt", "CreatedAt", "UpdatedAt")
    VALUES (gen_random_uuid(), p_email, p_token_hash, p_role,
        p_invited_by, 'Pending', p_expires_at, now(), now())
    RETURNING "Id" INTO v_id;
    RETURN v_id;
END; $$;