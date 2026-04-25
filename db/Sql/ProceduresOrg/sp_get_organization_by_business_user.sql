-- Lookup used by PurchaseService pre-PaymentIntent: given a BusinessUser
-- (the event organizer), return their Organization row (or no rows if the
-- BusinessUser is not yet linked).
CREATE OR REPLACE FUNCTION sp_get_organization_by_business_user(
    p_business_user_id uuid
) RETURNS SETOF organizations LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    RETURN QUERY
    SELECT o.*
    FROM organizations o
    INNER JOIN business_users bu ON bu."OrganizationId" = o."Id"
    WHERE bu."Id" = p_business_user_id
      AND o."ArchivedAt" IS NULL;
END; $$;
