-- Resets every Stripe-related column on an organization back to its
-- pre-onboarding state so a fresh `Account.Create` + onboarding link
-- can begin without colliding with stale state. Used by the developer
-- "clean restart" endpoint after the connected account has been
-- deleted at Stripe via `Account.Delete`.
--
-- Idempotent — re-running on an org that's already cleared is a no-op
-- update. Returns the number of rows affected (0 when the org id is
-- unknown, 1 on success).
CREATE OR REPLACE FUNCTION sp_clear_organization_stripe_account(
    p_organization_id uuid
) RETURNS int LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_rows int;
BEGIN
    UPDATE organizations
       SET "StripeConnectedAccountId" = NULL,
           "StripeChargesEnabled" = false,
           "StripePayoutsEnabled" = false,
           "StripeDetailsSubmitted" = false,
           "StripeOnboardedAt" = NULL,
           "StripeRequirementsDue" = NULL,
           "UpdatedAt" = now()
     WHERE "Id" = p_organization_id;

    GET DIAGNOSTICS v_rows = ROW_COUNT;
    RETURN v_rows;
END; $$;
