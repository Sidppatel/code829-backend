CREATE OR REPLACE FUNCTION sp_revoke_ticket_invite(p_ticket_id uuid)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE purchase_tickets SET
        "Status" = 'Unassigned',
        "InviteTokenHash" = NULL,
        "InviteExpiresAt" = NULL,
        "InvitedEmail" = NULL,
        "InviteSentAt" = NULL,
        "GuestUserId" = NULL,
        "ClaimedAt" = NULL,
        "UpdatedAt" = now()
    WHERE "Id" = p_ticket_id;
END; $$;
