CREATE OR REPLACE FUNCTION sp_get_locked_table_ids(p_event_id uuid)
RETURNS TABLE("Id" uuid) LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT DISTINCT b."TableId" FROM purchases b
    WHERE b."EventId" = p_event_id
      AND b."TableId" IS NOT NULL
      AND b."Status" IN ('Paid', 'CheckedIn', 'Pending')
    UNION
    SELECT t."Id" FROM tables t
    WHERE t."EventId" = p_event_id
      AND t."Status" = 'Locked'
      AND t."LockExpiresAt" > now();
$$;