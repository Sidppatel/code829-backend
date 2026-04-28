CREATE OR REPLACE FUNCTION sp_create_stripe_transaction(
    p_purchase_id uuid, p_intent_id text, p_amount_cents int,
    p_transfer_amount_cents int DEFAULT NULL, p_tax_calculation_id text DEFAULT NULL,
    p_currency text DEFAULT 'usd'
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO stripe_transactions ("Id", "PurchaseId", "PaymentIntentId", "Status",
        "AmountCents", "TransferAmountCents", "TaxCalculationId", "Currency", "CreatedAt", "UpdatedAt")
    VALUES (gen_random_uuid(), p_purchase_id, p_intent_id, 'RequiresConfirmation',
        p_amount_cents, p_transfer_amount_cents, p_tax_calculation_id, p_currency, now(), now())
    RETURNING "Id" INTO v_id;
    RETURN v_id;
END; $$;