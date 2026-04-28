CREATE OR REPLACE FUNCTION sp_claim_ticket(p_invite_hash text, p_guest_user_id uuid)
RETURNS TABLE("TicketId" uuid, "PurchaseId" uuid) LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    RETURN QUERY
    UPDATE purchase_tickets SET
        "GuestUserId" = p_guest_user_id, "ClaimedAt" = now(),
        "Status" = 'Claimed', "UpdatedAt" = now()
    WHERE "InviteTokenHash" = p_invite_hash AND "Status" = 'Invited' AND "InviteExpiresAt" > now()
    RETURNING purchase_tickets."Id" AS "TicketId", purchase_tickets."PurchaseId";
END; $$;