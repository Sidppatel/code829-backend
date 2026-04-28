CREATE OR REPLACE FUNCTION sp_set_stripe_tax_transaction_id(p_intent_id text, p_tax_transaction_id text)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE stripe_transactions SET
        "TaxTransactionId" = p_tax_transaction_id,
        "UpdatedAt" = now()
    WHERE "PaymentIntentId" = p_intent_id;
END; $$;