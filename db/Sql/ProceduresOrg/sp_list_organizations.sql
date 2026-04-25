-- Paginated, searchable list of organizations + member counts. Used by the
-- developer dashboard's organizations table.
--
--   p_search       case-insensitive trigram match against Name/LegalName.
--                  NULL or empty string returns all rows.
--   p_include_archived  when false, only non-archived rows are returned.
--   p_offset       zero-based row offset.
--   p_limit        max rows to return.
--
-- Returns the underlying organizations columns plus an aggregated MemberCount
-- so the FE can render the "Members" column without a per-row roundtrip.
CREATE OR REPLACE FUNCTION sp_list_organizations(
    p_search text DEFAULT NULL,
    p_include_archived boolean DEFAULT false,
    p_offset int DEFAULT 0,
    p_limit int DEFAULT 25
) RETURNS TABLE (
    "Id" uuid,
    "Name" text,
    "LegalName" text,
    "CountryCode" text,
    "StripeConnectedAccountId" text,
    "StripeChargesEnabled" boolean,
    "StripePayoutsEnabled" boolean,
    "StripeDetailsSubmitted" boolean,
    "StripeOnboardedAt" timestamptz,
    "StripeRequirementsDue" text,
    "ArchivedAt" timestamptz,
    "MemberCount" int,
    "CreatedAt" timestamptz,
    "UpdatedAt" timestamptz
) LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_search text;
BEGIN
    v_search := NULLIF(trim(coalesce(p_search, '')), '');

    RETURN QUERY
    SELECT
        o."Id",
        o."Name"::text,
        o."LegalName"::text,
        o."CountryCode"::text,
        o."StripeConnectedAccountId"::text,
        o."StripeChargesEnabled",
        o."StripePayoutsEnabled",
        o."StripeDetailsSubmitted",
        o."StripeOnboardedAt",
        o."StripeRequirementsDue"::text,
        o."ArchivedAt",
        COALESCE(mc.cnt, 0)::int AS "MemberCount",
        o."CreatedAt",
        o."UpdatedAt"
    FROM organizations o
    LEFT JOIN (
        SELECT "OrganizationId", count(*)::int AS cnt
        FROM business_users
        WHERE "OrganizationId" IS NOT NULL
        GROUP BY "OrganizationId"
    ) mc ON mc."OrganizationId" = o."Id"
    WHERE (p_include_archived OR o."ArchivedAt" IS NULL)
      AND (
        v_search IS NULL
        OR o."Name" ILIKE '%' || v_search || '%'
        OR o."LegalName" ILIKE '%' || v_search || '%'
      )
    ORDER BY o."CreatedAt" DESC
    OFFSET COALESCE(p_offset, 0)
    LIMIT  COALESCE(p_limit, 25);
END; $$;
