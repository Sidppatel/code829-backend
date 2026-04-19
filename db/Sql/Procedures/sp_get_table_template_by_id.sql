CREATE OR REPLACE FUNCTION sp_get_table_template_by_id(p_id uuid)
RETURNS SETOF table_templates
LANGUAGE sql STABLE AS $$
    SELECT * FROM table_templates WHERE "Id" = p_id;
$$;