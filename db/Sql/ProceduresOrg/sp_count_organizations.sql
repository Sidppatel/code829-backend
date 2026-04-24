-- Total row count for sp_list_organizations under the same filters. Returned
-- separately so the FE can render total + page count from one extra round-trip.
CREATE OR REPLACE FUNCTION sp_count_organizations(
    p_search text DEFAULT NULL,
    p_include_archived boolean DEFAULT false
) RETURNS int LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_search text;
    v_count int;
BEGIN
    v_search := NULLIF(trim(coalesce(p_search, '')), '');

    SELECT count(*)::int INTO v_count
    FROM organizations o
    WHERE (p_include_archived OR o."ArchivedAt" IS NULL)
      AND (
        v_search IS NULL
        OR o."Name" ILIKE '%' || v_search || '%'
        OR o."LegalName" ILIKE '%' || v_search || '%'
      );

    RETURN v_count;
END; $$;
