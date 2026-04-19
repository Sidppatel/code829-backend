CREATE OR REPLACE FUNCTION sp_unassign_admin_user_event(
    p_admin_user_id uuid, p_event_id uuid
) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM admin_user_events
    WHERE "AdminUserId" = p_admin_user_id AND "EventId" = p_event_id;
END; $$;
