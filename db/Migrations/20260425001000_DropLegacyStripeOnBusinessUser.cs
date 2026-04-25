using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Migration 3 of the Stripe Connect Express rollout. Drops
    /// business_users.StripeConnectedAccountId after the
    /// BackfillOrganizationsFromBusinessUsers migration has copied every value
    /// over to organizations.StripeConnectedAccountId. PurchaseService and the
    /// AdminStripeController + DeveloperController already read from the
    /// Organization side; this migration removes the legacy column so a future
    /// developer can't accidentally write back to the wrong place.
    ///
    /// Order of operations matters because v_business_users projects the
    /// column and Postgres refuses to drop a column that any view references.
    /// We:
    ///   1. Stash the current values into business_users_stripe_legacy_backup
    ///      (one row per BusinessUser that had a non-null acct id) so a rollback
    ///      after merge can be reconstructed without touching Stripe directly.
    ///   2. DROP VIEW v_business_users (the only consumer of the column).
    ///   3. DROP COLUMN.
    ///   4. CREATE OR REPLACE VIEW with the new column list — the .sql file in
    ///      db/Sql/Views is the source of truth for code review; the inline
    ///      SQL here mirrors it verbatim because views aren't scaffolded by EF.
    ///
    /// Down reverses the column drop but does NOT restore the backup data —
    /// dev is expected to stop-clear-start, prod restores from Supabase
    /// backups. The schema is reversible enough to satisfy `dotnet ef
    /// database update` in either direction.
    /// </remarks>
    public partial class DropLegacyStripeOnBusinessUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Backup table — append-only audit of acct ids that were on
            //    business_users at drop time. Keeps a record per
            //    (BusinessUserId, StripeConnectedAccountId) so post-mortem
            //    reconciliation against Stripe is straightforward.
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS business_users_stripe_legacy_backup (
                    ""BusinessUserId"" uuid NOT NULL,
                    ""StripeConnectedAccountId"" text NOT NULL,
                    ""BackedUpAt"" timestamptz NOT NULL DEFAULT now(),
                    PRIMARY KEY (""BusinessUserId"", ""StripeConnectedAccountId"")
                );

                INSERT INTO business_users_stripe_legacy_backup (""BusinessUserId"", ""StripeConnectedAccountId"")
                SELECT ""Id"", ""StripeConnectedAccountId""
                FROM business_users
                WHERE ""StripeConnectedAccountId"" IS NOT NULL
                ON CONFLICT DO NOTHING;
            ");

            // 2. Drop the dependent view so the column can be removed.
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_business_users;");

            // 3. Drop the column.
            migrationBuilder.DropColumn(
                name: "StripeConnectedAccountId",
                table: "business_users");

            // 4. Recreate the view without the dropped column. This SQL is the
            //    same body as db/Sql/Views/v_business_users.sql; both are kept
            //    in sync (the .sql file is the source for code review, this
            //    inline copy ships in the migration).
            migrationBuilder.Sql(@"
                CREATE OR REPLACE VIEW v_business_users AS
                SELECT
                    au.""Id"" AS ""BusinessUserId"", au.""Email"", au.""EmailHash"", au.""FirstName"", au.""LastName"",
                    au.""Role"", au.""IsActive"", au.""LastLoginAt"",
                    i.""StorageKey"" AS ""ImageStorageKey"",
                    au.""Phone"", au.""CreatedAt"", au.""UpdatedAt""
                FROM business_users au
                LEFT JOIN images i ON au.""ImageId"" = i.""Id"";
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_business_users;");

            migrationBuilder.AddColumn<string>(
                name: "StripeConnectedAccountId",
                table: "business_users",
                type: "text",
                nullable: true);

            // Restore the previous view shape (with the column re-included)
            // so a downgraded environment behaves like before.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE VIEW v_business_users AS
                SELECT
                    au.""Id"" AS ""BusinessUserId"", au.""Email"", au.""EmailHash"", au.""FirstName"", au.""LastName"",
                    au.""Role"", au.""IsActive"", au.""LastLoginAt"",
                    i.""StorageKey"" AS ""ImageStorageKey"",
                    au.""Phone"", au.""StripeConnectedAccountId"", au.""CreatedAt"", au.""UpdatedAt""
                FROM business_users au
                LEFT JOIN images i ON au.""ImageId"" = i.""Id"";
            ");
        }
    }
}
