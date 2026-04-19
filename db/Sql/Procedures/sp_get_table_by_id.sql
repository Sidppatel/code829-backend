CREATE OR REPLACE FUNCTION sp_get_table_by_id(p_id uuid)
RETURNS SETOF tables
LANGUAGE sql STABLE AS $$
    SELECT * FROM tables WHERE "Id" = p_id;
$$;