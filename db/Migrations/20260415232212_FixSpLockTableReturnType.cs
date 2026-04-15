using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class FixSpLockTableReturnType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix 42804: "structure of query does not match function result type"
            // RETURNS TABLE declared columns as text but actual table columns are varchar(N).
            // PostgreSQL's RETURN QUERY requires exact type match — add explicit casts.

            // sp_lock_table: tables."Label" is varchar(20), declared as text
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_lock_table(
    p_user_id uuid, p_event_id uuid, p_table_id uuid, p_hold_minutes int
) RETURNS TABLE(""Id"" uuid, ""Label"" text, ""LockExpiresAt"" timestamptz) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    UPDATE tables SET
        ""Status"" = 'Locked', ""LockedByUserId"" = p_user_id,
        ""LockExpiresAt"" = now() + (p_hold_minutes || ' minutes')::interval,
        ""UpdatedAt"" = now()
    WHERE tables.""Id"" = p_table_id AND tables.""EventId"" = p_event_id
      AND tables.""Status"" = 'Available' AND tables.""IsActive"" = true
    RETURNING tables.""Id"", tables.""Label""::text, tables.""LockExpiresAt"";
END; $$;
");

            // sp_consume_magic_link: magic_link_tokens."Email" is varchar(256), declared as text
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_consume_magic_link(p_token_hash text)
RETURNS TABLE (
    ""Id"" uuid, ""Email"" text, ""ExpiresAt"" timestamptz
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    UPDATE magic_link_tokens
    SET ""IsUsed"" = true, ""UsedAt"" = now(), ""UpdatedAt"" = now()
    WHERE ""TokenHash"" = p_token_hash AND ""IsUsed"" = false AND ""ExpiresAt"" > now()
    RETURNING magic_link_tokens.""Id"", magic_link_tokens.""Email""::text, magic_link_tokens.""ExpiresAt"";
END; $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert to originals (without casts)
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_lock_table(
    p_user_id uuid, p_event_id uuid, p_table_id uuid, p_hold_minutes int
) RETURNS TABLE(""Id"" uuid, ""Label"" text, ""LockExpiresAt"" timestamptz) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    UPDATE tables SET
        ""Status"" = 'Locked', ""LockedByUserId"" = p_user_id,
        ""LockExpiresAt"" = now() + (p_hold_minutes || ' minutes')::interval,
        ""UpdatedAt"" = now()
    WHERE tables.""Id"" = p_table_id AND tables.""EventId"" = p_event_id
      AND tables.""Status"" = 'Available' AND tables.""IsActive"" = true
    RETURNING tables.""Id"", tables.""Label"", tables.""LockExpiresAt"";
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_consume_magic_link(p_token_hash text)
RETURNS TABLE (
    ""Id"" uuid, ""Email"" text, ""ExpiresAt"" timestamptz
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    UPDATE magic_link_tokens
    SET ""IsUsed"" = true, ""UsedAt"" = now(), ""UpdatedAt"" = now()
    WHERE ""TokenHash"" = p_token_hash AND ""IsUsed"" = false AND ""ExpiresAt"" > now()
    RETURNING magic_link_tokens.""Id"", magic_link_tokens.""Email"", magic_link_tokens.""ExpiresAt"";
END; $$;
");
        }
    }
}
