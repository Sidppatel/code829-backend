CREATE OR REPLACE FUNCTION sp_list_existing_event_table_template_ids(p_event_id uuid)
RETURNS TABLE("TableTemplateId" uuid)
LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT "TableTemplateId" FROM event_tables
    WHERE "EventId" = p_event_id AND "TableTemplateId" IS NOT NULL;
$$;