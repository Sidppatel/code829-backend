CREATE OR REPLACE FUNCTION sp_get_developer_logs(
    p_severity text, p_path text, p_from timestamptz, p_to timestamptz,
    p_skip int, p_take int
) RETURNS SETOF v_developer_logs LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT * FROM v_developer_logs
    WHERE (p_severity IS NULL OR "Severity" = p_severity)
      AND (p_path IS NULL OR ("RequestPath" IS NOT NULL AND "RequestPath" ILIKE '%' || p_path || '%'))
      AND (p_from IS NULL OR "Timestamp" >= p_from)
      AND (p_to IS NULL OR "Timestamp" <= p_to)
    ORDER BY "Timestamp" DESC
    OFFSET p_skip LIMIT p_take;
$$;

CREATE OR REPLACE FUNCTION sp_count_developer_logs(
    p_severity text, p_path text, p_from timestamptz, p_to timestamptz
) RETURNS int LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT COUNT(*)::int FROM v_developer_logs
    WHERE (p_severity IS NULL OR "Severity" = p_severity)
      AND (p_path IS NULL OR ("RequestPath" IS NOT NULL AND "RequestPath" ILIKE '%' || p_path || '%'))
      AND (p_from IS NULL OR "Timestamp" >= p_from)
      AND (p_to IS NULL OR "Timestamp" <= p_to);
$$;
