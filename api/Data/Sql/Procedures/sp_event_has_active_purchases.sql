CREATE OR REPLACE FUNCTION sp_event_has_active_purchases(p_event_id uuid)
RETURNS boolean LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT EXISTS(
        SELECT 1 FROM purchases
        WHERE "EventId" = p_event_id
          AND "Status" NOT IN ('Cancelled', 'Refunded')
    );
$$;