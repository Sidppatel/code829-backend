CREATE OR REPLACE FUNCTION fn_audit_trigger() RETURNS trigger LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        INSERT INTO system_logs ("Id", "Timestamp", "Category", "Action", "Source",
            "EntityType", "EntityId", "BeforeJson", "AfterJson")
        VALUES (gen_random_uuid(), now(), 'EntityChange', 'Delete', TG_TABLE_NAME,
            TG_TABLE_NAME, (OLD."Id")::uuid, row_to_json(OLD)::text, NULL);
        RETURN OLD;
    ELSIF TG_OP = 'UPDATE' THEN
        INSERT INTO system_logs ("Id", "Timestamp", "Category", "Action", "Source",
            "EntityType", "EntityId", "BeforeJson", "AfterJson")
        VALUES (gen_random_uuid(), now(), 'EntityChange', 'Update', TG_TABLE_NAME,
            TG_TABLE_NAME, (NEW."Id")::uuid, row_to_json(OLD)::text, row_to_json(NEW)::text);
        RETURN NEW;
    ELSIF TG_OP = 'INSERT' THEN
        INSERT INTO system_logs ("Id", "Timestamp", "Category", "Action", "Source",
            "EntityType", "EntityId", "BeforeJson", "AfterJson")
        VALUES (gen_random_uuid(), now(), 'EntityChange', 'Insert', TG_TABLE_NAME,
            TG_TABLE_NAME, (NEW."Id")::uuid, NULL, row_to_json(NEW)::text);
        RETURN NEW;
    END IF;
    RETURN NULL;
END; $$;