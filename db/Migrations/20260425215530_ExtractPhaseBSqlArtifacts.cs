using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Phase B of SQL extraction: replaces inline LINQ aggregations in
    /// AdminEventsController.GetAll, AdminLayoutController.GetPurchaseInfoForEvent,
    /// AdminPurchasesController.GetStats, EventsController.GetFacets.
    /// </remarks>
    public partial class ExtractPhaseBSqlArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(MigrationSqlLoader.Load("v_event_table_stats.sql"));
            migrationBuilder.Sql(MigrationSqlLoader.Load("v_event_facets.sql"));
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_get_purchase_info_for_event.sql"));
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_get_purchase_stats.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_purchase_stats(uuid[], uuid);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_purchase_info_for_event(uuid);");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_event_facets;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_event_table_stats;");
        }
    }
}
