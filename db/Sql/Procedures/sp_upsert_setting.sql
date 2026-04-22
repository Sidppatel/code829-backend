CREATE OR REPLACE FUNCTION sp_upsert_setting(
    p_key text, p_value text, p_description text DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    INSERT INTO app_settings ("Id", "Key", "Value", "Description", "CreatedAt", "UpdatedAt")
    VALUES (gen_random_uuid(), p_key, p_value, p_description, now(), now())
    ON CONFLICT ("Key") DO UPDATE SET
        "Value" = EXCLUDED."Value",
        "Description" = COALESCE(EXCLUDED."Description", app_settings."Description"),
        "UpdatedAt" = now();
END; $$;