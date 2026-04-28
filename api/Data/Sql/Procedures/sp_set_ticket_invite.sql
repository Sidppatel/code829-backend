CREATE OR REPLACE FUNCTION sp_set_ticket_invite(
    p_ticket_id uuid, p_invite_hash text, p_email text, p_expires_at timestamptz
) RETURNS boolean LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_updated int;
BEGIN
    UPDATE purchase_tickets SET
        "InviteTokenHash" = p_invite_hash,
        "InvitedEmail" = p_email,
        "InviteSentAt" = now(),
        "InviteExpiresAt" = p_expires_at,
        "Status" = 'Invited',
        "GuestUserId" = NULL,
        "ClaimedAt" = NULL,
        "UpdatedAt" = now()
    WHERE "Id" = p_ticket_id
      AND "Status" IN ('Unassigned', 'Invited');
    GET DIAGNOSTICS v_updated = ROW_COUNT;
    RETURN v_updated > 0;
END; $$;
