CREATE OR REPLACE FUNCTION sp_lock_table(
    p_user_id uuid, p_event_id uuid, p_table_id uuid, p_hold_minutes int
) RETURNS TABLE("Id" uuid, "Label" text, "LockExpiresAt" timestamptz) LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    RETURN QUERY
    UPDATE tables SET
        "Status" = 'Locked', "LockedByUserId" = p_user_id,
        "LockExpiresAt" = now() + (p_hold_minutes || ' minutes')::interval,
        "UpdatedAt" = now()
    WHERE tables."Id" = p_table_id AND tables."EventId" = p_event_id
      AND tables."IsActive" = true
      AND (
          tables."Status" = 'Available'
          OR (tables."Status" = 'Locked' AND tables."LockExpiresAt" <= now())
      )
    RETURNING tables."Id", tables."Label"::text, tables."LockExpiresAt";
END; $$;