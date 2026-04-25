-- Returns one row per BusinessUser attached to the given organization. Used
-- by the developer organization-detail endpoint so add/remove member responses
-- can surface the live roster without a follow-up request.
--
--   p_organization_id  organization to fetch members for. Returns zero rows
--                      when the org id is unknown or the org is empty — the
--                      caller must verify the org exists separately.
--
-- DisplayName is "FirstName LastName" (trimmed, single-spaced) and is null
-- when both name parts are blank, matching the contract that
-- OrganizationMemberDto already exposes via the Stripe status endpoint.
CREATE OR REPLACE FUNCTION sp_get_organization_members(
    p_organization_id uuid
) RETURNS TABLE (
    "BusinessUserId" uuid,
    "Email" text,
    "DisplayName" text
) LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    RETURN QUERY
    SELECT
        bu."Id",
        bu."Email"::text,
        NULLIF(
            TRIM(REGEXP_REPLACE(
                CONCAT(COALESCE(bu."FirstName", ''), ' ', COALESCE(bu."LastName", '')),
                '\s+', ' ', 'g'
            )),
            ''
        )::text
    FROM business_users bu
    WHERE bu."OrganizationId" = p_organization_id
    ORDER BY bu."Email";
END; $$;
