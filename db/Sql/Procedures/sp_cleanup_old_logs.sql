CREATE OR REPLACE FUNCTION sp_cleanup_old_logs(
    p_dev_days int, p_admin_days int, p_system_days int
) RETURNS int LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_total int := 0; v_count int;
BEGIN
    DELETE FROM developer_logs WHERE "Timestamp" < now() - (p_dev_days || ' days')::interval;
    GET DIAGNOSTICS v_count = ROW_COUNT; v_total := v_total + v_count;
    DELETE FROM business_logs WHERE "Timestamp" < now() - (p_admin_days || ' days')::interval;
    GET DIAGNOSTICS v_count = ROW_COUNT; v_total := v_total + v_count;
    DELETE FROM system_logs WHERE "Timestamp" < now() - (p_system_days || ' days')::interval;
    GET DIAGNOSTICS v_count = ROW_COUNT; v_total := v_total + v_count;
    RETURN v_total;
END; $$;