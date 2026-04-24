-- Detaches a BusinessUser from an Organization (sets OrganizationId = NULL).
--
-- Safeguard: if the Organization has an active StripeConnectedAccountId AND
-- this BusinessUser is the LAST member (after detachment, no other linked
-- BusinessUsers remain), the procedure refuses. The caller must either:
--   1. Re-assign the BusinessUser to a different org first, or
--   2. Archive the Organization explicitly via sp_archive_organization (which
--      detaches the last member as part of its own logic).
--
-- This prevents orphaned Stripe accounts that no admin can manage.
CREATE OR REPLACE FUNCTION sp_remove_business_user_from_organization(
    p_business_user_id uuid
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_org_id uuid;
    v_stripe_acct text;
    v_remaining_members int;
BEGIN
    SELECT bu."OrganizationId", o."StripeConnectedAccountId"
      INTO v_org_id, v_stripe_acct
    FROM business_users bu
    LEFT JOIN organizations o ON o."Id" = bu."OrganizationId"
    WHERE bu."Id" = p_business_user_id
    FOR UPDATE OF bu;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'BusinessUser % not found', p_business_user_id
            USING ERRCODE = 'no_data_found';
    END IF;

    IF v_org_id IS NULL THEN
        RETURN;  -- already detached
    END IF;

    SELECT count(*) INTO v_remaining_members
    FROM business_users
    WHERE "OrganizationId" = v_org_id
      AND "Id" <> p_business_user_id;

    IF v_remaining_members = 0 AND v_stripe_acct IS NOT NULL THEN
        RAISE EXCEPTION 'Cannot detach last member from organization % — Stripe account % would be orphaned. Archive the organization first or move this member to another org.',
            v_org_id, v_stripe_acct
            USING ERRCODE = 'foreign_key_violation';
    END IF;

    UPDATE business_users
    SET "OrganizationId" = NULL,
        "UpdatedAt" = now()
    WHERE "Id" = p_business_user_id;
END; $$;
