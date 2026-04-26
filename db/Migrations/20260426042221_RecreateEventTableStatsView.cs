using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// AddTableLayoutSpans drops v_event_tables_summary CASCADE, which also drops the
    /// dependent v_event_table_stats. AddTableLayoutSpans recreates summary but not stats.
    /// This migration restores v_event_table_stats so AdminEventsController.GetAll works.
    /// </remarks>
    public partial class RecreateEventTableStatsView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(MigrationSqlLoader.Load("v_event_table_stats.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_event_table_stats;");
        }
    }
}
