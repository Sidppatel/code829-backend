CREATE OR REPLACE FUNCTION sp_deactivate_table_template(p_id uuid)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE table_templates SET "IsActive" = false, "UpdatedAt" = now()
    WHERE "Id" = p_id;
END; $$;