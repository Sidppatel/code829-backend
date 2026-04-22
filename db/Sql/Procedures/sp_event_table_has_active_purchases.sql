CREATE OR REPLACE FUNCTION sp_event_table_has_active_purchases(p_event_id uuid, p_event_table_id uuid)
RETURNS boolean LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT EXISTS(
        SELECT 1 FROM purchases b
        WHERE b."EventId" = p_event_id
          AND b."TableId" IS NOT NULL
          AND b."TableId" IN (SELECT "Id" FROM tables WHERE "EventTableId" = p_event_table_id)
          AND b."Status" IN ('Paid', 'CheckedIn', 'Pending')
    );
$$;