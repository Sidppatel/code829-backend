-- Returns the Stripe Connect status payload for the admin/dev dashboards.
-- Returns SETOF organizations so callers can use FromSqlRaw + entity tracking.
CREATE OR REPLACE FUNCTION sp_get_organization_stripe_status(
    p_id uuid
) RETURNS SETOF organizations LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    RETURN QUERY
    SELECT *
    FROM organizations
    WHERE "Id" = p_id
      AND "ArchivedAt" IS NULL;
END; $$;
