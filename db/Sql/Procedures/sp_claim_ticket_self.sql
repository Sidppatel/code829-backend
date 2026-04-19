CREATE OR REPLACE FUNCTION sp_claim_ticket_self(
    p_ticket_id uuid, p_user_id uuid
) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE purchase_tickets SET
        "GuestUserId" = p_user_id,
        "Status" = 'Claimed',
        "ClaimedAt" = now(),
        "InviteTokenHash" = NULL,
        "InviteExpiresAt" = NULL,
        "InvitedEmail" = NULL,
        "InviteSentAt" = NULL,
        "UpdatedAt" = now()
    WHERE "Id" = p_ticket_id;
END; $$;
