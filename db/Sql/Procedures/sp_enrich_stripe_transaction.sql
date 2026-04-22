CREATE OR REPLACE FUNCTION sp_enrich_stripe_transaction(
    p_intent_id text, p_total_charged_cents int, p_tax_amount_cents int, p_stripe_fees_cents int
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE stripe_transactions SET
        "TotalChargedCents" = p_total_charged_cents,
        "TaxAmountCents" = p_tax_amount_cents,
        "StripeFeesCents" = p_stripe_fees_cents,
        "UpdatedAt" = now()
    WHERE "PaymentIntentId" = p_intent_id;
END; $$;