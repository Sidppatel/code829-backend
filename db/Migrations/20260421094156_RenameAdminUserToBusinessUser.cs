using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class RenameAdminUserToBusinessUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data-preserving in-place rename. Renames tables/columns/indexes/constraints
            // rather than drop+create so existing rows survive a migration on prod DBs
            // (Supabase, local seeded copies, etc.).

            // 1. Drop dependent views. They will be reinstalled at the end of this
            //    migration from db/Sql/Views/*.sql (already rewritten to reference the
            //    new business_* schema). CASCADE handles views-of-views.
            migrationBuilder.Sql(@"
                DROP VIEW IF EXISTS
                    v_admin_logs,
                    v_admin_user_events,
                    v_admin_users,
                    v_event_summary,
                    v_events,
                    v_invitations,
                    v_system_logs,
                    v_device_sessions,
                    v_purchases
                CASCADE;");

            // 2. Drop old admin-prefixed stored procedures (reinstalled under business_*
            //    names by the Sql.Procedures loader at the end). Preserve the three
            //    portal-aligned admin-log SPs — their bodies were rewritten to operate on
            //    business_logs but the function names stay.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE r record;
                BEGIN
                    FOR r IN
                        SELECT p.oid::regprocedure::text AS sig
                        FROM pg_proc p
                        JOIN pg_namespace n ON n.oid = p.pronamespace
                        WHERE n.nspname = 'public'
                          AND p.proname ~ '^sp_.*admin'
                          AND p.proname NOT IN (
                              'sp_create_admin_log',
                              'sp_get_admin_logs',
                              'sp_count_admin_logs'
                          )
                    LOOP
                        EXECUTE 'DROP FUNCTION IF EXISTS ' || r.sig || ' CASCADE';
                    END LOOP;
                END $$;");

            // 3. Drop SPs whose signatures (param types, param names, or return shape)
            //    changed in this release — CREATE OR REPLACE can't rewrite those, so they
            //    must be dropped before the loader reinstalls them.
            //    The three sp_*_admin_log* SPs keep their public names (portal contract)
            //    but rename their p_admin_user_id param → p_business_user_id, which PG
            //    rejects with 42P13 unless dropped first.
            migrationBuilder.Sql(@"
                DROP FUNCTION IF EXISTS sp_claim_ticket_self(uuid, uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_create_event(text, text, text, text, text, timestamp with time zone, timestamp with time zone, text, boolean, text, integer, integer, integer, integer, integer, integer, uuid, uuid, timestamp with time zone) CASCADE;
                DROP FUNCTION IF EXISTS sp_list_events_for_staff(uuid, integer) CASCADE;
                DROP FUNCTION IF EXISTS sp_list_staff_for_event(uuid) CASCADE;
                DROP FUNCTION IF EXISTS sp_set_ticket_invite(uuid, text, text, timestamp with time zone) CASCADE;
                DROP FUNCTION IF EXISTS sp_staff_can_access_event(uuid, uuid, integer) CASCADE;
                DROP FUNCTION IF EXISTS sp_create_admin_log(text, uuid, text, uuid, text, text, text) CASCADE;
                DROP FUNCTION IF EXISTS sp_get_admin_logs(text, text, timestamptz, timestamptz, integer, integer) CASCADE;
                DROP FUNCTION IF EXISTS sp_count_admin_logs(text, text, timestamptz, timestamptz) CASCADE;");

            // 4. Rename cross-table FK columns (rows untouched; PG updates dependent
            //    constraints and indexes in place via their attnum references).
            migrationBuilder.RenameColumn(
                name: "InvitedByAdminUserId",
                table: "invitations",
                newName: "InvitedByBusinessUserId");

            migrationBuilder.RenameColumn(
                name: "AdminUserId",
                table: "events",
                newName: "BusinessUserId");

            migrationBuilder.RenameColumn(
                name: "AdminUserId",
                table: "device_sessions",
                newName: "BusinessUserId");

            migrationBuilder.RenameColumn(
                name: "AdminUserId",
                table: "developer_logs",
                newName: "BusinessUserId");

            // 5. Rename indexes on the renamed cross-table FK columns.
            migrationBuilder.RenameIndex(
                name: "IX_invitations_InvitedByAdminUserId",
                table: "invitations",
                newName: "IX_invitations_InvitedByBusinessUserId");

            migrationBuilder.RenameIndex(
                name: "IX_events_AdminUserId",
                table: "events",
                newName: "IX_events_BusinessUserId");

            migrationBuilder.RenameIndex(
                name: "IX_device_sessions_AdminUserId",
                table: "device_sessions",
                newName: "IX_device_sessions_BusinessUserId");

            // 6. Rename columns inside the admin_* tables (still pre-table-rename).
            migrationBuilder.RenameColumn(
                name: "AdminUserId",
                table: "admin_user_events",
                newName: "BusinessUserId");

            migrationBuilder.RenameColumn(
                name: "AssignedByAdminUserId",
                table: "admin_user_events",
                newName: "AssignedByBusinessUserId");

            migrationBuilder.RenameColumn(
                name: "AdminUserId",
                table: "admin_password_reset_tokens",
                newName: "BusinessUserId");

            migrationBuilder.RenameColumn(
                name: "AdminUserId",
                table: "admin_logs",
                newName: "BusinessUserId");

            // 7. Recreate CK_device_sessions_UserType with the new column name.
            //    (PG auto-updates the expression on column rename, but dropping and
            //    re-adding is defensive and matches the parallel preserve-SQL path.)
            migrationBuilder.DropCheckConstraint(
                name: "CK_device_sessions_UserType",
                table: "device_sessions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_device_sessions_UserType",
                table: "device_sessions",
                sql: "(\"UserId\" IS NOT NULL AND \"BusinessUserId\" IS NULL) OR (\"UserId\" IS NULL AND \"BusinessUserId\" IS NOT NULL)");

            // 8. Rename the four admin_* tables. FK references from other tables are
            //    preserved automatically — PG stores FK targets by OID, not by name.
            migrationBuilder.RenameTable(name: "admin_users", newName: "business_users");
            migrationBuilder.RenameTable(name: "admin_user_events", newName: "business_user_events");
            migrationBuilder.RenameTable(name: "admin_password_reset_tokens", newName: "business_password_reset_tokens");
            migrationBuilder.RenameTable(name: "admin_logs", newName: "business_logs");

            // 9. Rename PK / CK / FK constraint NAMES. EF has no RenameConstraint API
            //    so this goes through raw SQL. Renaming the constraint only — no
            //    structural change — which is why rows and FK integrity stay intact.
            migrationBuilder.Sql(@"
                ALTER TABLE business_users                 RENAME CONSTRAINT ""PK_admin_users""                 TO ""PK_business_users"";
                ALTER TABLE business_user_events           RENAME CONSTRAINT ""PK_admin_user_events""           TO ""PK_business_user_events"";
                ALTER TABLE business_password_reset_tokens RENAME CONSTRAINT ""PK_admin_password_reset_tokens"" TO ""PK_business_password_reset_tokens"";
                ALTER TABLE business_logs                  RENAME CONSTRAINT ""PK_admin_logs""                  TO ""PK_business_logs"";

                ALTER TABLE business_users                 RENAME CONSTRAINT ""CK_admin_users_Role""                  TO ""CK_business_users_Role"";
                ALTER TABLE business_password_reset_tokens RENAME CONSTRAINT ""CK_admin_password_reset_tokens_Usage"" TO ""CK_business_password_reset_tokens_Usage"";

                ALTER TABLE business_users                 RENAME CONSTRAINT ""FK_admin_users_images_ImageId""                          TO ""FK_business_users_images_ImageId"";
                ALTER TABLE business_user_events           RENAME CONSTRAINT ""FK_admin_user_events_admin_users_AdminUserId""           TO ""FK_business_user_events_business_users_BusinessUserId"";
                ALTER TABLE business_user_events           RENAME CONSTRAINT ""FK_admin_user_events_admin_users_AssignedByAdminUserId"" TO ""FK_business_user_events_business_users_AssignedByBusinessUserId"";
                ALTER TABLE business_user_events           RENAME CONSTRAINT ""FK_admin_user_events_events_EventId""                    TO ""FK_business_user_events_events_EventId"";
                ALTER TABLE business_password_reset_tokens RENAME CONSTRAINT ""FK_admin_password_reset_tokens_admin_users_AdminUserId"" TO ""FK_business_password_reset_tokens_business_users_BusinessUserId"";
                ALTER TABLE device_sessions                RENAME CONSTRAINT ""FK_device_sessions_admin_users_AdminUserId""             TO ""FK_device_sessions_business_users_BusinessUserId"";
                ALTER TABLE events                         RENAME CONSTRAINT ""FK_events_admin_users_AdminUserId""                      TO ""FK_events_business_users_BusinessUserId"";
                ALTER TABLE invitations                    RENAME CONSTRAINT ""FK_invitations_admin_users_InvitedByAdminUserId""        TO ""FK_invitations_business_users_InvitedByBusinessUserId"";");

            // 10. Rename non-PK indexes on the renamed tables. (PKs were renamed above
            //     via their constraint names; PG auto-renames the underlying index.)
            migrationBuilder.RenameIndex(
                name: "IX_admin_users_Email",
                newName: "IX_business_users_Email");
            migrationBuilder.RenameIndex(
                name: "IX_admin_users_EmailHash",
                newName: "IX_business_users_EmailHash");
            migrationBuilder.RenameIndex(
                name: "IX_admin_users_ImageId",
                newName: "IX_business_users_ImageId");
            migrationBuilder.RenameIndex(
                name: "IX_admin_logs_Action",
                newName: "IX_business_logs_Action");
            migrationBuilder.RenameIndex(
                name: "IX_admin_logs_Timestamp",
                newName: "IX_business_logs_Timestamp");
            migrationBuilder.RenameIndex(
                name: "IX_admin_password_reset_tokens_AdminUserId",
                newName: "IX_business_password_reset_tokens_BusinessUserId");
            migrationBuilder.RenameIndex(
                name: "IX_admin_password_reset_tokens_ExpiresAt",
                newName: "IX_business_password_reset_tokens_ExpiresAt");
            migrationBuilder.RenameIndex(
                name: "IX_admin_password_reset_tokens_TokenHash",
                newName: "IX_business_password_reset_tokens_TokenHash");
            migrationBuilder.RenameIndex(
                name: "IX_admin_user_events_AdminUserId_EventId",
                newName: "IX_business_user_events_BusinessUserId_EventId");
            migrationBuilder.RenameIndex(
                name: "IX_admin_user_events_AssignedByAdminUserId",
                newName: "IX_business_user_events_AssignedByBusinessUserId");
            migrationBuilder.RenameIndex(
                name: "IX_admin_user_events_EventId",
                newName: "IX_business_user_events_EventId");

            // 11. Reinstall views and stored procedures with business_* bodies.
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.Views");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.Procedures");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Rename migration 20260421094156_RenameAdminUserToBusinessUser is forward-only. " +
                "The admin_* SQL view/procedure bodies are no longer in the tree, so a clean " +
                "reversal would require re-authoring them. Restore from a backup if revert needed.");
        }
    }
}
