CREATE OR REPLACE FUNCTION sp_cancel_purchase(p_purchase_id uuid) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE purchases SET "Status" = 'Cancelled', "UpdatedAt" = now()
    WHERE "Id" = p_purchase_id;

    UPDATE tables SET "Status" = 'Available', "LockedByUserId" = NULL,
        "LockExpiresAt" = NULL, "UpdatedAt" = now()
    WHERE "Id" IN (SELECT "TableId" FROM purchase_tables WHERE "PurchaseId" = p_purchase_id)
      AND "Status" IN ('Locked', 'Booked');
END; $$;