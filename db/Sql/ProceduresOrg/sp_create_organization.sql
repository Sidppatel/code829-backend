CREATE OR REPLACE FUNCTION sp_create_organization(
    p_name text,
    p_legal_name text DEFAULT NULL,
    p_country_code text DEFAULT 'US'
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO organizations (
        "Name", "LegalName", "CountryCode",
        "StripeChargesEnabled", "StripePayoutsEnabled", "StripeDetailsSubmitted",
        "CreatedAt", "UpdatedAt"
    )
    VALUES (
        p_name, p_legal_name, COALESCE(p_country_code, 'US'),
        false, false, false,
        now(), now()
    )
    RETURNING "Id" INTO v_id;
    RETURN v_id;
END; $$;
