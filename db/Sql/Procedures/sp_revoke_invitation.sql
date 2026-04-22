CREATE OR REPLACE FUNCTION sp_revoke_invitation(p_id uuid)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE invitations
    SET "Status" = 'Revoked',
        "UpdatedAt" = now()
    WHERE "Id" = p_id AND "Status" = 'Pending';
END; $$;