CREATE OR REPLACE FUNCTION sp_update_organization(
    p_id uuid,
    p_name text DEFAULT NULL,
    p_legal_name text DEFAULT NULL,
    p_country_code text DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE organizations SET
        "Name"        = COALESCE(p_name,         "Name"),
        "LegalName"   = COALESCE(p_legal_name,   "LegalName"),
        "CountryCode" = COALESCE(p_country_code, "CountryCode"),
        "UpdatedAt"   = now()
    WHERE "Id" = p_id
      AND "ArchivedAt" IS NULL;
END; $$;
