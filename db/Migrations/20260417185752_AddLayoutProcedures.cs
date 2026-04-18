using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class AddLayoutProcedures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ─── Table Template procedures ─────────────────────────────

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_table_template(
    p_name text, p_capacity int, p_shape text, p_color text, p_price_cents int
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO table_templates (""Id"", ""Name"", ""DefaultCapacity"", ""DefaultShape"",
        ""DefaultColor"", ""DefaultPriceCents"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
    VALUES (gen_random_uuid(), p_name, p_capacity, p_shape,
        p_color, p_price_cents, true, now(), now())
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_update_table_template(
    p_id uuid, p_name text, p_capacity int, p_shape text,
    p_color text, p_price_cents int, p_is_active bool
) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE table_templates SET
        ""Name"" = COALESCE(p_name, ""Name""),
        ""DefaultCapacity"" = COALESCE(p_capacity, ""DefaultCapacity""),
        ""DefaultShape"" = COALESCE(p_shape, ""DefaultShape""),
        ""DefaultColor"" = p_color,
        ""DefaultPriceCents"" = COALESCE(p_price_cents, ""DefaultPriceCents""),
        ""IsActive"" = COALESCE(p_is_active, ""IsActive""),
        ""UpdatedAt"" = now()
    WHERE ""Id"" = p_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_deactivate_table_template(p_id uuid)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE table_templates SET ""IsActive"" = false, ""UpdatedAt"" = now()
    WHERE ""Id"" = p_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_get_table_template_by_id(p_id uuid)
RETURNS SETOF table_templates
LANGUAGE sql STABLE AS $$
    SELECT * FROM table_templates WHERE ""Id"" = p_id;
$$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_list_active_table_templates()
RETURNS SETOF table_templates
LANGUAGE sql STABLE AS $$
    SELECT * FROM table_templates ORDER BY ""Name"";
$$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_list_active_table_templates_by_ids(p_ids uuid[])
RETURNS SETOF table_templates
LANGUAGE sql STABLE AS $$
    SELECT * FROM table_templates WHERE ""Id"" = ANY(p_ids) AND ""IsActive"" = true;
$$;
");

            // ─── Event read for layout ─────────────────────────────────

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_get_event_by_id_for_layout(p_id uuid)
RETURNS SETOF events
LANGUAGE sql STABLE AS $$
    SELECT * FROM events WHERE ""Id"" = p_id;
$$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_update_event_grid(p_id uuid, p_grid_rows int, p_grid_cols int)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE events SET ""GridRows"" = p_grid_rows, ""GridCols"" = p_grid_cols, ""UpdatedAt"" = now()
    WHERE ""Id"" = p_id;
END; $$;
");

            // ─── Event Table procedures ────────────────────────────────
            // Note: sp_create_event_table is defined in the Initial migration and reused here.

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_update_event_table(
    p_id uuid, p_label text, p_capacity int, p_shape text, p_color text,
    p_price_cents int, p_is_active bool
) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE event_tables SET
        ""Label"" = COALESCE(p_label, ""Label""),
        ""Capacity"" = COALESCE(p_capacity, ""Capacity""),
        ""Shape"" = COALESCE(p_shape, ""Shape""),
        ""Color"" = CASE WHEN p_color IS NOT NULL THEN p_color ELSE ""Color"" END,
        ""PriceCents"" = COALESCE(p_price_cents, ""PriceCents""),
        ""IsActive"" = COALESCE(p_is_active, ""IsActive""),
        ""UpdatedAt"" = now()
    WHERE ""Id"" = p_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_delete_event_table(p_id uuid)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM tables WHERE ""EventTableId"" = p_id;
    DELETE FROM event_tables WHERE ""Id"" = p_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_get_event_table_by_id(p_id uuid)
RETURNS SETOF event_tables
LANGUAGE sql STABLE AS $$
    SELECT * FROM event_tables WHERE ""Id"" = p_id;
$$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_list_event_tables_for_event(p_event_id uuid)
RETURNS SETOF event_tables
LANGUAGE sql STABLE AS $$
    SELECT * FROM event_tables WHERE ""EventId"" = p_event_id ORDER BY ""Label"";
$$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_list_existing_event_table_template_ids(p_event_id uuid)
RETURNS TABLE(""TableTemplateId"" uuid)
LANGUAGE sql STABLE AS $$
    SELECT ""TableTemplateId"" FROM event_tables
    WHERE ""EventId"" = p_event_id AND ""TableTemplateId"" IS NOT NULL;
$$;
");

            // ─── Table (individual) procedures ─────────────────────────
            // Note: sp_create_table is defined in the Initial migration and reused here.

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_update_table(
    p_id uuid, p_label text, p_event_table_id uuid,
    p_grid_row int, p_grid_col int, p_is_active bool, p_sort_order int
) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE tables SET
        ""Label"" = COALESCE(p_label, ""Label""),
        ""EventTableId"" = COALESCE(p_event_table_id, ""EventTableId""),
        ""GridRow"" = COALESCE(p_grid_row, ""GridRow""),
        ""GridCol"" = COALESCE(p_grid_col, ""GridCol""),
        ""IsActive"" = COALESCE(p_is_active, ""IsActive""),
        ""SortOrder"" = COALESCE(p_sort_order, ""SortOrder""),
        ""UpdatedAt"" = now()
    WHERE ""Id"" = p_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_delete_table(p_id uuid)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM tables WHERE ""Id"" = p_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_get_table_by_id(p_id uuid)
RETURNS SETOF tables
LANGUAGE sql STABLE AS $$
    SELECT * FROM tables WHERE ""Id"" = p_id;
$$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_list_tables_for_event(p_event_id uuid)
RETURNS SETOF tables
LANGUAGE sql STABLE AS $$
    SELECT * FROM tables WHERE ""EventId"" = p_event_id ORDER BY ""SortOrder"";
$$;
");

            // ─── Layout lock / purchase check procedures ────────────────

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_event_has_active_purchases(p_event_id uuid)
RETURNS boolean LANGUAGE sql STABLE AS $$
    SELECT EXISTS(
        SELECT 1 FROM purchases
        WHERE ""EventId"" = p_event_id
          AND ""Status"" NOT IN ('Cancelled', 'Refunded')
    );
$$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_event_table_has_active_purchases(p_event_id uuid, p_event_table_id uuid)
RETURNS boolean LANGUAGE sql STABLE AS $$
    SELECT EXISTS(
        SELECT 1 FROM purchases b
        WHERE b.""EventId"" = p_event_id
          AND b.""TableId"" IS NOT NULL
          AND b.""TableId"" IN (SELECT ""Id"" FROM tables WHERE ""EventTableId"" = p_event_table_id)
          AND b.""Status"" IN ('Paid', 'CheckedIn', 'Pending')
    );
$$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_event_table_has_locked_tables(p_event_table_id uuid)
RETURNS boolean LANGUAGE sql STABLE AS $$
    SELECT EXISTS(
        SELECT 1 FROM tables
        WHERE ""EventTableId"" = p_event_table_id
          AND ""Status"" = 'Locked'
          AND ""LockExpiresAt"" > now()
    );
$$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_get_locked_table_ids(p_event_id uuid)
RETURNS TABLE(""Id"" uuid) LANGUAGE sql STABLE AS $$
    SELECT DISTINCT b.""TableId"" FROM purchases b
    WHERE b.""EventId"" = p_event_id
      AND b.""TableId"" IS NOT NULL
      AND b.""Status"" IN ('Paid', 'CheckedIn', 'Pending')
    UNION
    SELECT t.""Id"" FROM tables t
    WHERE t.""EventId"" = p_event_id
      AND t.""Status"" = 'Locked'
      AND t.""LockExpiresAt"" > now();
$$;
");

            // ─── Save layout (atomic bulk upsert/delete) ───────────────

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_save_event_layout(
    p_event_id uuid, p_grid_rows int, p_grid_cols int,
    p_tables jsonb, p_locked_ids uuid[]
) RETURNS void LANGUAGE plpgsql AS $$
DECLARE
    v_request_ids uuid[];
    v_table jsonb;
    v_id uuid;
BEGIN
    UPDATE events SET ""GridRows"" = p_grid_rows, ""GridCols"" = p_grid_cols, ""UpdatedAt"" = now()
    WHERE ""Id"" = p_event_id;

    -- Collect the ids present in the request so we can delete anything not in it.
    SELECT COALESCE(array_agg((t->>'Id')::uuid) FILTER (WHERE t->>'Id' IS NOT NULL), '{}')
    INTO v_request_ids
    FROM jsonb_array_elements(p_tables) AS t;

    -- Delete tables that are not in the request and not locked.
    DELETE FROM tables
    WHERE ""EventId"" = p_event_id
      AND ""Id"" <> ALL(v_request_ids)
      AND ""Id"" <> ALL(p_locked_ids);

    -- Upsert each table in the request (skip locked ids for updates).
    FOR v_table IN SELECT * FROM jsonb_array_elements(p_tables)
    LOOP
        v_id := NULLIF(v_table->>'Id', '')::uuid;
        IF v_id IS NOT NULL AND v_id = ANY(p_locked_ids) THEN
            CONTINUE;
        END IF;

        IF v_id IS NOT NULL AND EXISTS(SELECT 1 FROM tables WHERE ""Id"" = v_id) THEN
            UPDATE tables SET
                ""Label"" = v_table->>'Label',
                ""GridRow"" = (v_table->>'GridRow')::int,
                ""GridCol"" = (v_table->>'GridCol')::int,
                ""IsActive"" = (v_table->>'IsActive')::bool,
                ""SortOrder"" = (v_table->>'SortOrder')::int,
                ""EventTableId"" = (v_table->>'EventTableId')::uuid,
                ""UpdatedAt"" = now()
            WHERE ""Id"" = v_id;
        ELSE
            INSERT INTO tables (""Id"", ""EventId"", ""EventTableId"", ""Label"",
                ""GridRow"", ""GridCol"", ""IsActive"", ""SortOrder"", ""Status"",
                ""CreatedAt"", ""UpdatedAt"")
            VALUES (
                COALESCE(v_id, gen_random_uuid()), p_event_id,
                (v_table->>'EventTableId')::uuid,
                v_table->>'Label',
                (v_table->>'GridRow')::int,
                (v_table->>'GridCol')::int,
                (v_table->>'IsActive')::bool,
                (v_table->>'SortOrder')::int,
                'Available', now(), now()
            );
        END IF;
    END LOOP;
END; $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_save_event_layout(uuid, int, int, jsonb, uuid[]);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_locked_table_ids(uuid);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_event_table_has_locked_tables(uuid);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_event_table_has_active_purchases(uuid, uuid);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_event_has_active_purchases(uuid);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_list_tables_for_event(uuid);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_table_by_id(uuid);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_delete_table(uuid);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_update_table(uuid, text, uuid, int, int, bool, int);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_list_existing_event_table_template_ids(uuid);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_list_event_tables_for_event(uuid);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_event_table_by_id(uuid);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_delete_event_table(uuid);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_update_event_table(uuid, text, int, text, text, int, bool);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_update_event_grid(uuid, int, int);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_event_by_id_for_layout(uuid);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_list_active_table_templates_by_ids(uuid[]);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_list_active_table_templates();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_table_template_by_id(uuid);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_deactivate_table_template(uuid);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_update_table_template(uuid, text, int, text, text, int, bool);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_create_table_template(text, int, text, text, int);");
        }
    }
}
