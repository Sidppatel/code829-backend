using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class AddEventAndFeedbackDeleteProcedures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_delete_event(p_id uuid)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM events WHERE ""Id"" = p_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_delete_feedback(p_id uuid)
RETURNS boolean LANGUAGE plpgsql AS $$
DECLARE v_exists boolean;
BEGIN
    SELECT EXISTS(SELECT 1 FROM feedbacks WHERE ""Id"" = p_id) INTO v_exists;
    IF v_exists THEN
        DELETE FROM feedbacks WHERE ""Id"" = p_id;
    END IF;
    RETURN v_exists;
END; $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_delete_feedback(uuid);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_delete_event(uuid);");
        }
    }
}
