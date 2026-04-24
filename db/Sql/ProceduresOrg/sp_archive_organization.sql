-- Soft-deletes an Organization by setting ArchivedAt = now(). Archived
-- organizations cannot host new events, are filtered out of the active
-- lookup SPs (sp_get_organization_*, sp_get_organization_by_business_user),
-- and retain audit history (Stripe transactions, payouts, etc.).
--
-- Detaches all member BusinessUsers as part of the archive. Idempotent —
-- archiving an already-archived org is a no-op.
CREATE OR REPLACE FUNCTION sp_archive_organization(
    p_id uuid
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_archived_at timestamptz;
BEGIN
    SELECT "ArchivedAt" INTO v_archived_at
    FROM organizations
    WHERE "Id" = p_id
    FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Organization % not found', p_id
            USING ERRCODE = 'no_data_found';
    END IF;

    IF v_archived_at IS NOT NULL THEN
        RETURN;  -- already archived
    END IF;

    UPDATE business_users
    SET "OrganizationId" = NULL,
        "UpdatedAt" = now()
    WHERE "OrganizationId" = p_id;

    UPDATE organizations
    SET "ArchivedAt" = now(),
        "UpdatedAt" = now()
    WHERE "Id" = p_id;
END; $$;
