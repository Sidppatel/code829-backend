CREATE OR REPLACE FUNCTION sp_list_active_table_templates()
RETURNS SETOF table_templates
LANGUAGE sql STABLE AS $$
    SELECT * FROM table_templates ORDER BY "Name";
$$;