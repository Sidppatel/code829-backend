-- Bulk-updates the four Stripe status flags + requirements_due JSON in
-- response to an account.updated webhook. Sets StripeOnboardedAt the first
-- time DetailsSubmitted flips true (and never overwrites it on subsequent
-- updates).
CREATE OR REPLACE FUNCTION sp_update_organization_stripe_status(
    p_stripe_account_id text,
    p_charges_enabled boolean,
    p_payouts_enabled boolean,
    p_details_submitted boolean,
    p_requirements_due_json jsonb DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE organizations
    SET "StripeChargesEnabled"   = p_charges_enabled,
        "StripePayoutsEnabled"   = p_payouts_enabled,
        "StripeDetailsSubmitted" = p_details_submitted,
        "StripeRequirementsDue"  = COALESCE(p_requirements_due_json, "StripeRequirementsDue"),
        "StripeOnboardedAt"      = CASE
            WHEN "StripeOnboardedAt" IS NULL AND p_details_submitted = true THEN now()
            ELSE "StripeOnboardedAt"
        END,
        "UpdatedAt" = now()
    WHERE "StripeConnectedAccountId" = p_stripe_account_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'No organization found with Stripe account %', p_stripe_account_id
            USING ERRCODE = 'no_data_found';
    END IF;
END; $$;
