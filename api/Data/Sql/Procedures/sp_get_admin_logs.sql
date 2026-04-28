CREATE OR REPLACE FUNCTION sp_get_admin_logs(
    p_action text, p_entity_type text, p_from timestamptz, p_to timestamptz,
    p_skip int, p_take int
) RETURNS SETOF v_business_logs LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT * FROM v_business_logs
    WHERE (p_action IS NULL OR "Action" ILIKE '%' || p_action || '%')
      AND (p_entity_type IS NULL OR "EntityType" = p_entity_type)
      AND (p_from IS NULL OR "Timestamp" >= p_from)
      AND (p_to IS NULL OR "Timestamp" <= p_to)
    ORDER BY "Timestamp" DESC
    OFFSET p_skip LIMIT p_take;
$$;

CREATE OR REPLACE FUNCTION sp_count_admin_logs(
    p_action text, p_entity_type text, p_from timestamptz, p_to timestamptz
) RETURNS int LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT COUNT(*)::int FROM v_business_logs
    WHERE (p_action IS NULL OR "Action" ILIKE '%' || p_action || '%')
      AND (p_entity_type IS NULL OR "EntityType" = p_entity_type)
      AND (p_from IS NULL OR "Timestamp" >= p_from)
      AND (p_to IS NULL OR "Timestamp" <= p_to);
$$;
