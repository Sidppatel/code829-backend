CREATE OR REPLACE FUNCTION sp_accept_invitation(p_id uuid)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE invitations
    SET "Status" = 'Accepted',
        "AcceptedAt" = now(),
        "UpdatedAt" = now()
    WHERE "Id" = p_id;
END; $$;