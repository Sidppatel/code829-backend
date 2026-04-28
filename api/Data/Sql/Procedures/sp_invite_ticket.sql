CREATE OR REPLACE FUNCTION sp_invite_ticket(
    p_ticket_id uuid, p_invite_hash text, p_email text, p_expires_at timestamptz
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE purchase_tickets SET
        "InviteTokenHash" = p_invite_hash, "InvitedEmail" = p_email,
        "InviteSentAt" = now(), "InviteExpiresAt" = p_expires_at,
        "Status" = 'Invited', "UpdatedAt" = now()
    WHERE "Id" = p_ticket_id;
END; $$;