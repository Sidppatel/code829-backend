CREATE OR REPLACE FUNCTION sp_create_developer_log(
    p_severity text, p_message text, p_exception_type text, p_stack_trace text,
    p_request_path text, p_request_method text, p_status_code int,
    p_user_id uuid, p_ip text, p_correlation_id text, p_metadata_json text
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO developer_logs ("Id", "Timestamp", "Severity", "Message", "ExceptionType",
        "StackTrace", "RequestPath", "RequestMethod", "StatusCode", "UserId",
        "IpAddress", "CorrelationId", "MetadataJson")
    VALUES (gen_random_uuid(), now(), p_severity, p_message, p_exception_type, p_stack_trace,
        p_request_path, p_request_method, p_status_code, p_user_id,
        p_ip, p_correlation_id, p_metadata_json)
    RETURNING "Id" INTO v_id;
    RETURN v_id;
END; $$;