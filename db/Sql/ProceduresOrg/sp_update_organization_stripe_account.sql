-- Persists the Stripe Connect account ID on first AccountCreate.
-- Called once per Organization, after Stripe.Account.Create returns "acct_...".
-- Refuses to overwrite an already-set account (organizations are 1:1 with
-- Stripe accounts; rotating accounts is out of scope and risky for payouts).
CREATE OR REPLACE FUNCTION sp_update_organization_stripe_account(
    p_id uuid,
    p_stripe_account_id text
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_existing text;
BEGIN
    SELECT "StripeConnectedAccountId" INTO v_existing
    FROM organizations
    WHERE "Id" = p_id
    FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Organization % not found', p_id
            USING ERRCODE = 'no_data_found';
    END IF;

    IF v_existing IS NOT NULL AND v_existing <> p_stripe_account_id THEN
        RAISE EXCEPTION 'Organization % already has Stripe account % — refusing to overwrite with %',
            p_id, v_existing, p_stripe_account_id
            USING ERRCODE = 'unique_violation';
    END IF;

    UPDATE organizations
    SET "StripeConnectedAccountId" = p_stripe_account_id,
        "UpdatedAt" = now()
    WHERE "Id" = p_id;
END; $$;
