using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Phase A of SQL extraction: replaces inline LINQ aggregations in
    /// AdminDashboardController + DeveloperDashboardController with views and
    /// functions. Bodies live as embedded SQL files under db/Sql/.
    ///
    /// CREATE OR REPLACE keeps each artifact idempotent — re-running the migration
    /// after a body tweak is safe.
    /// </remarks>
    public partial class ExtractDashboardSqlArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(MigrationSqlLoader.Load("v_admin_dashboard_stats.sql"));
            migrationBuilder.Sql(MigrationSqlLoader.Load("v_top_events_revenue.sql"));
            migrationBuilder.Sql(MigrationSqlLoader.Load("v_purchases_by_status.sql"));
            migrationBuilder.Sql(MigrationSqlLoader.Load("v_events_by_category.sql"));
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_get_next_event_dashboard.sql"));
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_get_event_recent_purchases.sql"));
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_get_monthly_report_summary.sql"));
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_get_monthly_report_by_event.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_monthly_report_by_event(int, int);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_monthly_report_summary(int, int);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_event_recent_purchases(uuid, int);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_next_event_dashboard(timestamptz);");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_events_by_category;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_purchases_by_status;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_top_events_revenue;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_admin_dashboard_stats;");
        }
    }
}
