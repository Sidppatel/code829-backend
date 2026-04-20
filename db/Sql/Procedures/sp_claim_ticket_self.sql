CREATE OR REPLACE FUNCTION sp_claim_ticket_self(
    p_ticket_id uuid, p_user_id uuid
) RETURNS boolean LANGUAGE plpgsql AS $$
DECLARE v_updated int;
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
    WHERE "Id" = p_ticket_id
      AND "Status" IN ('Unassigned', 'Invited');
    GET DIAGNOSTICS v_updated = ROW_COUNT;
    RETURN v_updated > 0;
END; $$;
