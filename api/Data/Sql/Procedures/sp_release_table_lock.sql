CREATE OR REPLACE FUNCTION sp_release_table_lock(
    p_user_id uuid, p_event_id uuid, p_table_id uuid
) RETURNS bool LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE tables SET "Status" = 'Available', "LockedByUserId" = NULL,
        "LockExpiresAt" = NULL, "UpdatedAt" = now()
    WHERE "Id" = p_table_id AND "EventId" = p_event_id
      AND "LockedByUserId" = p_user_id AND "Status" = 'Locked';
    RETURN FOUND;
END; $$;