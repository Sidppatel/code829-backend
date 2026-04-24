-- Attaches a BusinessUser to an Organization. Used by the developer
-- "Merge admins into one organization" flow.
--   * 404-style error if either side doesn't exist or the org is archived.
--   * No-op if the BusinessUser is already attached to the target org.
--   * Re-parents (overwrites OrganizationId) if the BusinessUser was attached
--     to a different org. The caller is responsible for archiving the
--     source org if it was the last member (sp_remove_business_user_from_organization
--     covers the symmetric case).
CREATE OR REPLACE FUNCTION sp_add_business_user_to_organization(
    p_business_user_id uuid,
    p_organization_id uuid
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_archived_at timestamptz;
    v_current_org uuid;
BEGIN
    SELECT "ArchivedAt" INTO v_archived_at
    FROM organizations
    WHERE "Id" = p_organization_id
    FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Organization % not found', p_organization_id
            USING ERRCODE = 'no_data_found';
    END IF;

    IF v_archived_at IS NOT NULL THEN
        RAISE EXCEPTION 'Organization % is archived — cannot add members', p_organization_id
            USING ERRCODE = 'invalid_parameter_value';
    END IF;

    SELECT "OrganizationId" INTO v_current_org
    FROM business_users
    WHERE "Id" = p_business_user_id
    FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'BusinessUser % not found', p_business_user_id
            USING ERRCODE = 'no_data_found';
    END IF;

    IF v_current_org IS NOT DISTINCT FROM p_organization_id THEN
        RETURN;  -- already attached, nothing to do
    END IF;

    UPDATE business_users
    SET "OrganizationId" = p_organization_id,
        "UpdatedAt" = now()
    WHERE "Id" = p_business_user_id;
END; $$;
