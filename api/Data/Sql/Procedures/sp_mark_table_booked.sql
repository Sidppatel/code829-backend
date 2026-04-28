CREATE OR REPLACE FUNCTION sp_mark_table_booked(p_table_id uuid) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE tables SET "Status" = 'Booked', "LockedByUserId" = NULL,
        "LockExpiresAt" = NULL, "UpdatedAt" = now()
    WHERE "Id" = p_table_id;
END; $$;