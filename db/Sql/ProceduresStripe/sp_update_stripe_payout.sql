-- Upsert one Stripe payout row keyed on the Stripe payout id. Called from
-- both payout.created (status='pending'|'in_transit') and payout.paid
-- (status='paid' + PaidAt) — the SP hides the create vs update split.
--
-- Resolves OrganizationId from the source Stripe account id. If the org is
-- unknown the SP raises so the webhook handler can clear the dedupe key and
-- let Stripe retry once the org is wired up.
CREATE OR REPLACE FUNCTION sp_update_stripe_payout(
    p_stripe_payout_id text,
    p_stripe_account_id text,
    p_amount_cents int,
    p_currency text,
    p_status text,
    p_arrival_date timestamptz,
    p_paid_at timestamptz,
    p_raw_event jsonb
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_org_id uuid;
    v_id uuid;
BEGIN
    SELECT "Id" INTO v_org_id
    FROM organizations
    WHERE "StripeConnectedAccountId" = p_stripe_account_id;

    IF v_org_id IS NULL THEN
        RAISE EXCEPTION 'No organization found with Stripe account %', p_stripe_account_id
            USING ERRCODE = 'no_data_found';
    END IF;

    INSERT INTO stripe_payouts (
        "Id", "StripePayoutId", "OrganizationId",
        "AmountCents", "Currency", "Status",
        "ArrivalDate", "PaidAt", "RawEvent",
        "CreatedAt", "UpdatedAt"
    )
    VALUES (
        gen_random_uuid(), p_stripe_payout_id, v_org_id,
        p_amount_cents, COALESCE(p_currency, 'usd'), p_status,
        p_arrival_date, p_paid_at, p_raw_event,
        now(), now()
    )
    ON CONFLICT ("StripePayoutId") DO UPDATE
    SET "Status"      = EXCLUDED."Status",
        -- Never overwrite PaidAt once set — payout.paid is final.
        "PaidAt"      = COALESCE(stripe_payouts."PaidAt", EXCLUDED."PaidAt"),
        "ArrivalDate" = COALESCE(EXCLUDED."ArrivalDate", stripe_payouts."ArrivalDate"),
        "RawEvent"    = EXCLUDED."RawEvent",
        "UpdatedAt"   = now()
    RETURNING "Id" INTO v_id;

    RETURN v_id;
END; $$;
