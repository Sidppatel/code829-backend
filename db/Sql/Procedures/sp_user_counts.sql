CREATE OR REPLACE FUNCTION sp_user_counts()
RETURNS TABLE("Total" integer, "Active" integer, "NewThisMonth" integer)
LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT
        COUNT(*)::integer AS "Total",
        COUNT(*) FILTER (WHERE "IsActive" = true)::integer AS "Active",
        COUNT(*) FILTER (WHERE "CreatedAt" >= date_trunc('month', now()))::integer AS "NewThisMonth"
    FROM users;
$$;