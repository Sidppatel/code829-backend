CREATE OR REPLACE FUNCTION sp_delete_event_ticket_type(p_id uuid) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE event_ticket_types SET "IsActive" = false, "UpdatedAt" = now() WHERE "Id" = p_id;
END; $$;