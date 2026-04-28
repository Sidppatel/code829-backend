CREATE OR REPLACE FUNCTION sp_unassign_business_user_event(
    p_business_user_id uuid, p_event_id uuid
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    DELETE FROM business_user_events
    WHERE "BusinessUserId" = p_business_user_id AND "EventId" = p_event_id;
END; $$;
