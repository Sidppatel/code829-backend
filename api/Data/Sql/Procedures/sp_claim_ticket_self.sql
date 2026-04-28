CREATE OR REPLACE FUNCTION sp_claim_ticket_self(
    p_ticket_id uuid, p_user_id uuid
) RETURNS TABLE("Success" boolean, "Message" text) LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_purchase_id uuid;
    v_status text;
    v_already_count int;
    v_updated int;
BEGIN
    SELECT "PurchaseId", "Status"::text
        INTO v_purchase_id, v_status
        FROM purchase_tickets
        WHERE "Id" = p_ticket_id
        FOR UPDATE;

    IF v_purchase_id IS NULL THEN
        RETURN QUERY SELECT false, 'Ticket not found';
        RETURN;
    END IF;

    IF v_status NOT IN ('Unassigned', 'Invited') THEN
        RETURN QUERY SELECT false, 'This ticket has already been claimed. Revoke it first.';
        RETURN;
    END IF;

    SELECT COUNT(*) INTO v_already_count
        FROM purchase_tickets
        WHERE "PurchaseId" = v_purchase_id
          AND "GuestUserId" = p_user_id
          AND "Id" <> p_ticket_id
          AND "Status" IN ('Claimed', 'CheckedIn');

    IF v_already_count > 0 THEN
        RETURN QUERY SELECT false, 'You already have a ticket on this purchase. One ticket per person.';
        RETURN;
    END IF;

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

    IF v_updated = 0 THEN
        RETURN QUERY SELECT false, 'This ticket has already been claimed. Revoke it first.';
        RETURN;
    END IF;

    RETURN QUERY SELECT true, 'Ticket claimed';
END; $$;
