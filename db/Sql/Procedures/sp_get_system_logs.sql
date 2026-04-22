CREATE OR REPLACE FUNCTION sp_get_system_logs(
    p_after timestamptz, p_category text, p_entity_type text, p_take int
) RETURNS SETOF v_system_logs LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT * FROM v_system_logs
    WHERE (p_after IS NULL OR "Timestamp" < p_after)
      AND (p_category IS NULL OR "Category" = p_category)
      AND (p_entity_type IS NULL OR "EntityType" = p_entity_type)
    ORDER BY "Timestamp" DESC
    LIMIT p_take;
$$;
