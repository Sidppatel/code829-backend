CREATE OR REPLACE FUNCTION sp_update_stripe_transaction_status(p_intent_id text, p_status text)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE stripe_transactions SET
        "Status" = p_status,
        "PaidAt" = CASE WHEN p_status IN ('Succeeded','Refunded') AND "PaidAt" IS NULL THEN now() ELSE "PaidAt" END,
        "RefundedAt" = CASE WHEN p_status = 'Refunded' THEN now() ELSE "RefundedAt" END,
        "UpdatedAt" = now()
    WHERE "PaymentIntentId" = p_intent_id;
END; $$;