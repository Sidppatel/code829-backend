using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Phase 1 layout restructure: adds row/col span to tables, event_tables, table_templates;
    /// refreshes v_tables and v_event_tables_summary views; replaces 7 layout SPs with span-aware
    /// signatures; adds new sp_check_grid_overlap. SP DDL is inline (rather than reading from
    /// db/Sql/Procedures/) so the historical DropLegacyLogTables.LoadAll(Sql.Procedures) keeps
    /// working on fresh DB builds — that loader runs before this migration.
    /// </remarks>
    public partial class AddTableLayoutSpans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Schema columns ────────────────────────────────────────────

            migrationBuilder.AddColumn<int>(
                name: "RowSpan", table: "tables",
                type: "integer", nullable: false, defaultValue: 1);
            migrationBuilder.AddColumn<int>(
                name: "ColSpan", table: "tables",
                type: "integer", nullable: false, defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "RowSpan", table: "event_tables",
                type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "ColSpan", table: "event_tables",
                type: "integer", nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultRowSpan", table: "table_templates",
                type: "integer", nullable: false, defaultValue: 1);
            migrationBuilder.AddColumn<int>(
                name: "DefaultColSpan", table: "table_templates",
                type: "integer", nullable: false, defaultValue: 1);

            migrationBuilder.Sql("""
                ALTER TABLE tables ADD CONSTRAINT ck_tables_row_span_min CHECK ("RowSpan" >= 1);
                ALTER TABLE tables ADD CONSTRAINT ck_tables_col_span_min CHECK ("ColSpan" >= 1);
                ALTER TABLE event_tables ADD CONSTRAINT ck_event_tables_row_span_min CHECK ("RowSpan" IS NULL OR "RowSpan" >= 1);
                ALTER TABLE event_tables ADD CONSTRAINT ck_event_tables_col_span_min CHECK ("ColSpan" IS NULL OR "ColSpan" >= 1);
                ALTER TABLE table_templates ADD CONSTRAINT ck_table_templates_default_row_span_min CHECK ("DefaultRowSpan" >= 1);
                ALTER TABLE table_templates ADD CONSTRAINT ck_table_templates_default_col_span_min CHECK ("DefaultColSpan" >= 1);
            """);

            // ── 2. View refreshes ────────────────────────────────────────────

            migrationBuilder.Sql("""
                DROP VIEW IF EXISTS v_tables CASCADE;
                CREATE VIEW v_tables AS
                SELECT
                    t."Id" AS "TableId", t."EventId", t."EventTableId",
                    t."Label", t."GridRow", t."GridCol",
                    t."RowSpan", t."ColSpan",
                    t."IsActive", t."SortOrder",
                    t."Status"::text,
                    t."LockedByUserId", t."LockExpiresAt",
                    t."CreatedAt", t."UpdatedAt",
                    et."Capacity", et."Shape"::text, et."Color",
                    et."PriceCents", et."PlatformFeeCents",
                    et."PriceCents" + COALESCE(et."PlatformFeeCents", 0) AS "TotalPriceCents",
                    et."Label" AS "EventTableLabel"
                FROM tables t
                JOIN event_tables et ON t."EventTableId" = et."Id";
            """);

            migrationBuilder.Sql("""
                DROP VIEW IF EXISTS v_event_tables_summary CASCADE;
                CREATE VIEW v_event_tables_summary AS
                SELECT
                    et."Id" AS "EventTableId", et."EventId", et."Label", et."Capacity",
                    et."Shape"::text, et."Color", et."PriceCents", et."PlatformFeeCents",
                    COALESCE(et."RowSpan", tt."DefaultRowSpan", 1)::int AS "DefaultRowSpan",
                    COALESCE(et."ColSpan", tt."DefaultColSpan", 1)::int AS "DefaultColSpan",
                    et."IsActive",
                    COALESCE(ts.total, 0)::int AS "TotalTables",
                    COALESCE(ts.available, 0)::int AS "AvailableTables",
                    COALESCE(ts.locked, 0)::int AS "LockedTables",
                    COALESCE(ts.booked, 0)::int AS "BookedTables"
                FROM event_tables et
                LEFT JOIN table_templates tt ON et."TableTemplateId" = tt."Id"
                LEFT JOIN LATERAL (
                    SELECT
                        COUNT(*)::int AS total,
                        COUNT(*) FILTER (WHERE t."Status" = 'Available' AND t."IsActive")::int AS available,
                        COUNT(*) FILTER (WHERE t."Status" = 'Locked')::int AS locked,
                        COUNT(*) FILTER (WHERE t."Status" = 'Booked')::int AS booked
                    FROM tables t WHERE t."EventTableId" = et."Id"
                ) ts ON true;
            """);

            // ── 3. Layout SPs (drop old + create new with span params) ───────

            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS sp_create_table(uuid, uuid, text, int, int, int);
                CREATE OR REPLACE FUNCTION sp_create_table(
                    p_event_table_id uuid, p_event_id uuid, p_label text,
                    p_grid_row int, p_grid_col int, p_sort_order int,
                    p_row_span int, p_col_span int
                ) RETURNS uuid LANGUAGE plpgsql
                    SET search_path = public, extensions, pg_catalog
                AS $$
                DECLARE v_id uuid;
                BEGIN
                    INSERT INTO tables ("Id", "EventTableId", "EventId", "Label",
                        "GridRow", "GridCol", "RowSpan", "ColSpan",
                        "SortOrder", "IsActive", "Status", "CreatedAt", "UpdatedAt")
                    VALUES (gen_random_uuid(), p_event_table_id, p_event_id, p_label,
                        p_grid_row, p_grid_col,
                        COALESCE(p_row_span, 1), COALESCE(p_col_span, 1),
                        p_sort_order, true, 'Available', now(), now())
                    RETURNING "Id" INTO v_id;
                    RETURN v_id;
                END; $$;
            """);

            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS sp_update_table(uuid, text, uuid, int, int, bool, int);
                CREATE OR REPLACE FUNCTION sp_update_table(
                    p_id uuid, p_label text, p_event_table_id uuid,
                    p_grid_row int, p_grid_col int, p_is_active bool, p_sort_order int,
                    p_row_span int, p_col_span int
                ) RETURNS void LANGUAGE plpgsql
                    SET search_path = public, extensions, pg_catalog
                AS $$
                BEGIN
                    UPDATE tables SET
                        "Label" = COALESCE(p_label, "Label"),
                        "EventTableId" = COALESCE(p_event_table_id, "EventTableId"),
                        "GridRow" = COALESCE(p_grid_row, "GridRow"),
                        "GridCol" = COALESCE(p_grid_col, "GridCol"),
                        "RowSpan" = COALESCE(p_row_span, "RowSpan"),
                        "ColSpan" = COALESCE(p_col_span, "ColSpan"),
                        "IsActive" = COALESCE(p_is_active, "IsActive"),
                        "SortOrder" = COALESCE(p_sort_order, "SortOrder"),
                        "UpdatedAt" = now()
                    WHERE "Id" = p_id;
                END; $$;
            """);

            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS sp_create_event_table(uuid, text, int, text, text, int, int, uuid);
                CREATE OR REPLACE FUNCTION sp_create_event_table(
                    p_event_id uuid, p_label text, p_capacity int, p_shape text, p_color text,
                    p_price_cents int, p_platform_fee_cents int, p_template_id uuid,
                    p_row_span int, p_col_span int
                ) RETURNS uuid LANGUAGE plpgsql
                    SET search_path = public, extensions, pg_catalog
                AS $$
                DECLARE v_id uuid;
                BEGIN
                    INSERT INTO event_tables ("Id", "EventId", "Label", "Capacity", "Shape", "Color",
                        "PriceCents", "PlatformFeeCents", "RowSpan", "ColSpan",
                        "IsActive", "TableTemplateId", "CreatedAt", "UpdatedAt")
                    VALUES (gen_random_uuid(), p_event_id, p_label, p_capacity, p_shape, p_color,
                        p_price_cents, p_platform_fee_cents, p_row_span, p_col_span,
                        true, p_template_id, now(), now())
                    RETURNING "Id" INTO v_id;
                    RETURN v_id;
                END; $$;
            """);

            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS sp_update_event_table(uuid, text, int, text, text, int, bool);
                CREATE OR REPLACE FUNCTION sp_update_event_table(
                    p_id uuid, p_label text, p_capacity int, p_shape text, p_color text,
                    p_price_cents int, p_is_active bool, p_platform_fee_cents int,
                    p_row_span int, p_col_span int
                ) RETURNS void LANGUAGE plpgsql
                    SET search_path = public, extensions, pg_catalog
                AS $$
                BEGIN
                    UPDATE event_tables SET
                        "Label" = COALESCE(p_label, "Label"),
                        "Capacity" = COALESCE(p_capacity, "Capacity"),
                        "Shape" = COALESCE(p_shape, "Shape"),
                        "Color" = CASE WHEN p_color IS NOT NULL THEN p_color ELSE "Color" END,
                        "PriceCents" = COALESCE(p_price_cents, "PriceCents"),
                        "PlatformFeeCents" = COALESCE(p_platform_fee_cents, "PlatformFeeCents"),
                        "RowSpan" = COALESCE(p_row_span, "RowSpan"),
                        "ColSpan" = COALESCE(p_col_span, "ColSpan"),
                        "IsActive" = COALESCE(p_is_active, "IsActive"),
                        "UpdatedAt" = now()
                    WHERE "Id" = p_id;
                END; $$;
            """);

            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS sp_create_table_template(text, int, text, text, int);
                CREATE OR REPLACE FUNCTION sp_create_table_template(
                    p_name text, p_capacity int, p_shape text, p_color text, p_price_cents int,
                    p_default_row_span int, p_default_col_span int
                ) RETURNS uuid LANGUAGE plpgsql
                    SET search_path = public, extensions, pg_catalog
                AS $$
                DECLARE v_id uuid;
                BEGIN
                    INSERT INTO table_templates ("Id", "Name", "DefaultCapacity", "DefaultShape",
                        "DefaultColor", "DefaultPriceCents", "DefaultRowSpan", "DefaultColSpan",
                        "IsActive", "CreatedAt", "UpdatedAt")
                    VALUES (gen_random_uuid(), p_name, p_capacity, p_shape,
                        p_color, p_price_cents,
                        COALESCE(p_default_row_span, 1), COALESCE(p_default_col_span, 1),
                        true, now(), now())
                    RETURNING "Id" INTO v_id;
                    RETURN v_id;
                END; $$;
            """);

            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS sp_update_table_template(uuid, text, int, text, text, int, bool);
                CREATE OR REPLACE FUNCTION sp_update_table_template(
                    p_id uuid, p_name text, p_capacity int, p_shape text,
                    p_color text, p_price_cents int, p_is_active bool,
                    p_default_row_span int, p_default_col_span int
                ) RETURNS void LANGUAGE plpgsql
                    SET search_path = public, extensions, pg_catalog
                AS $$
                BEGIN
                    UPDATE table_templates SET
                        "Name" = COALESCE(p_name, "Name"),
                        "DefaultCapacity" = COALESCE(p_capacity, "DefaultCapacity"),
                        "DefaultShape" = COALESCE(p_shape, "DefaultShape"),
                        "DefaultColor" = p_color,
                        "DefaultPriceCents" = COALESCE(p_price_cents, "DefaultPriceCents"),
                        "DefaultRowSpan" = COALESCE(p_default_row_span, "DefaultRowSpan"),
                        "DefaultColSpan" = COALESCE(p_default_col_span, "DefaultColSpan"),
                        "IsActive" = COALESCE(p_is_active, "IsActive"),
                        "UpdatedAt" = now()
                    WHERE "Id" = p_id;
                END; $$;
            """);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION sp_save_event_layout(
                    p_event_id uuid, p_grid_rows int, p_grid_cols int,
                    p_tables jsonb, p_locked_ids uuid[]
                ) RETURNS void LANGUAGE plpgsql
                    SET search_path = public, extensions, pg_catalog
                AS $$
                DECLARE
                    v_request_ids uuid[];
                    v_table jsonb;
                    v_id uuid;
                BEGIN
                    UPDATE events SET "GridRows" = p_grid_rows, "GridCols" = p_grid_cols, "UpdatedAt" = now()
                    WHERE "Id" = p_event_id;

                    SELECT COALESCE(array_agg((t->>'Id')::uuid) FILTER (WHERE t->>'Id' IS NOT NULL), '{}')
                    INTO v_request_ids
                    FROM jsonb_array_elements(p_tables) AS t;

                    DELETE FROM tables
                    WHERE "EventId" = p_event_id
                      AND "Id" <> ALL(v_request_ids)
                      AND "Id" <> ALL(p_locked_ids);

                    FOR v_table IN SELECT * FROM jsonb_array_elements(p_tables)
                    LOOP
                        v_id := NULLIF(v_table->>'Id', '')::uuid;
                        IF v_id IS NOT NULL AND v_id = ANY(p_locked_ids) THEN
                            CONTINUE;
                        END IF;

                        IF v_id IS NOT NULL AND EXISTS(SELECT 1 FROM tables WHERE "Id" = v_id) THEN
                            UPDATE tables SET
                                "Label" = v_table->>'Label',
                                "GridRow" = (v_table->>'GridRow')::int,
                                "GridCol" = (v_table->>'GridCol')::int,
                                "RowSpan" = COALESCE((v_table->>'RowSpan')::int, "RowSpan", 1),
                                "ColSpan" = COALESCE((v_table->>'ColSpan')::int, "ColSpan", 1),
                                "IsActive" = (v_table->>'IsActive')::bool,
                                "SortOrder" = (v_table->>'SortOrder')::int,
                                "EventTableId" = (v_table->>'EventTableId')::uuid,
                                "UpdatedAt" = now()
                            WHERE "Id" = v_id;
                        ELSE
                            INSERT INTO tables ("Id", "EventId", "EventTableId", "Label",
                                "GridRow", "GridCol", "RowSpan", "ColSpan",
                                "IsActive", "SortOrder", "Status",
                                "CreatedAt", "UpdatedAt")
                            VALUES (
                                COALESCE(v_id, gen_random_uuid()), p_event_id,
                                (v_table->>'EventTableId')::uuid,
                                v_table->>'Label',
                                (v_table->>'GridRow')::int,
                                (v_table->>'GridCol')::int,
                                COALESCE((v_table->>'RowSpan')::int, 1),
                                COALESCE((v_table->>'ColSpan')::int, 1),
                                (v_table->>'IsActive')::bool,
                                (v_table->>'SortOrder')::int,
                                'Available', now(), now()
                            );
                        END IF;
                    END LOOP;
                END; $$;
            """);

            // ── 4. New overlap-check SP ──────────────────────────────────────

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION sp_check_grid_overlap(
                    p_event_id uuid, p_skip_table_id uuid
                ) RETURNS TABLE("TableAId" uuid, "TableBId" uuid)
                LANGUAGE sql
                    SET search_path = public, extensions, pg_catalog
                AS $$
                    SELECT a."Id", b."Id"
                    FROM tables a
                    JOIN tables b
                      ON a."EventId" = b."EventId"
                      AND a."Id" < b."Id"
                      AND a."IsActive" AND b."IsActive"
                    WHERE a."EventId" = p_event_id
                      AND (p_skip_table_id IS NULL
                           OR (a."Id" <> p_skip_table_id AND b."Id" <> p_skip_table_id))
                      AND a."GridRow" < b."GridRow" + b."RowSpan"
                      AND b."GridRow" < a."GridRow" + a."RowSpan"
                      AND a."GridCol" < b."GridCol" + b."ColSpan"
                      AND b."GridCol" < a."GridCol" + a."ColSpan";
                $$;
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_check_grid_overlap(uuid, uuid);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_save_event_layout(uuid, int, int, jsonb, uuid[]);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_update_table_template(uuid, text, int, text, text, int, bool, int, int);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_create_table_template(text, int, text, text, int, int, int);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_update_event_table(uuid, text, int, text, text, int, bool, int, int, int);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_create_event_table(uuid, text, int, text, text, int, int, uuid, int, int);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_update_table(uuid, text, uuid, int, int, bool, int, int, int);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_create_table(uuid, uuid, text, int, int, int, int, int);");

            migrationBuilder.Sql("DROP VIEW IF EXISTS v_event_tables_summary CASCADE;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_tables CASCADE;");

            migrationBuilder.DropColumn(name: "DefaultColSpan", table: "table_templates");
            migrationBuilder.DropColumn(name: "DefaultRowSpan", table: "table_templates");
            migrationBuilder.DropColumn(name: "ColSpan", table: "event_tables");
            migrationBuilder.DropColumn(name: "RowSpan", table: "event_tables");
            migrationBuilder.DropColumn(name: "ColSpan", table: "tables");
            migrationBuilder.DropColumn(name: "RowSpan", table: "tables");
        }
    }
}
