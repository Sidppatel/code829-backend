using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class SeparateAdminAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ─── Step 1: Create admin_users table ────────────────────
            migrationBuilder.CreateTable(
                name: "admin_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EmailHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LastName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AvatarPath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    StripeConnectedAccountId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_users", x => x.Id);
                    table.CheckConstraint("CK_admin_users_Role", "\"Role\" IN ('Staff','Admin','Developer')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_users_Email",
                table: "admin_users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_users_EmailHash",
                table: "admin_users",
                column: "EmailHash",
                unique: true);

            // ─── Step 2: Migrate admin/staff/dev users to admin_users ─
            migrationBuilder.Sql(@"
INSERT INTO admin_users (""Id"", ""Email"", ""EmailHash"", ""FirstName"", ""LastName"",
    ""PasswordHash"", ""Role"", ""IsActive"", ""LastLoginAt"", ""AvatarPath"", ""Phone"",
    ""StripeConnectedAccountId"", ""CreatedAt"", ""UpdatedAt"")
SELECT ""Id"", ""Email"", ""EmailHash"", ""FirstName"", ""LastName"",
    '$2a$12$placeholder.needs.reset.via.seeder.or.admin',
    CASE ""Role""
        WHEN 'Developer' THEN 'Developer'
        WHEN 'Admin' THEN 'Admin'
        WHEN 'Staff' THEN 'Staff'
    END,
    ""IsActive"", ""LastLoginAt"", ""AvatarPath"", ""Phone"",
    ""StripeConnectedAccountId"", ""CreatedAt"", ""UpdatedAt""
FROM users
WHERE ""Role"" IN ('Developer', 'Admin', 'Staff');
");

            // ─── Step 3: Update events FK from users to admin_users ──
            migrationBuilder.DropForeignKey(
                name: "FK_events_users_OrganizerId",
                table: "events");

            migrationBuilder.AddForeignKey(
                name: "FK_events_admin_users_OrganizerId",
                table: "events",
                column: "OrganizerId",
                principalTable: "admin_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ─── Step 4: Update device_sessions for dual user type ───
            migrationBuilder.AddColumn<Guid>(
                name: "AdminUserId",
                table: "device_sessions",
                type: "uuid",
                nullable: true);

            // Migrate existing admin sessions before adding constraints
            migrationBuilder.Sql(@"
UPDATE device_sessions SET ""AdminUserId"" = ""UserId"", ""UserId"" = NULL
WHERE ""UserId"" IN (SELECT ""Id"" FROM admin_users);
");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "device_sessions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_device_sessions_AdminUserId",
                table: "device_sessions",
                column: "AdminUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_device_sessions_UserType",
                table: "device_sessions",
                sql: "(\"UserId\" IS NOT NULL AND \"AdminUserId\" IS NULL) OR (\"UserId\" IS NULL AND \"AdminUserId\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_device_sessions_admin_users_AdminUserId",
                table: "device_sessions",
                column: "AdminUserId",
                principalTable: "admin_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // ─── Step 5: Remove admin/staff/dev rows from users ──────
            migrationBuilder.Sql(@"
DELETE FROM users WHERE ""Role"" IN ('Developer', 'Admin', 'Staff');
");

            // ─── Step 6: Remove Role column from users ───────────────
            migrationBuilder.DropCheckConstraint(
                name: "CK_users_Role",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "users");

            // ─── Step 7: Update v_user_profile to remove Role ────────
            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW v_user_profile AS
SELECT
    u.""Id"", u.""Email"", u.""FirstName"", u.""LastName"",
    u.""IsActive"", u.""LastLoginAt"",
    u.""Phone"", u.""OptInLocationEmail"", u.""HasCompletedOnboarding"",
    u.""AvatarPath"", u.""CreatedAt"",
    a.""Line1"" AS ""AddressLine1"",
    a.""City"", a.""State"", a.""ZipCode""
FROM users u
LEFT JOIN addresses a ON u.""AddressId"" = a.""Id"";
");

            // ─── Step 8: Update event views to join admin_users ──────
            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW v_events AS
SELECT
    e.""Id"", e.""Title"", e.""Slug"", e.""Description"", e.""Status""::text,
    COALESCE(e.""Category""::text, '') AS ""Category"",
    e.""StartDate"", e.""EndDate"", e.""ImagePath"", e.""IsFeatured"",
    e.""LayoutMode""::text, e.""MaxCapacity"", e.""PricePerPersonCents"",
    e.""PlatformFeePercent"", e.""PlatformFeeCents"",
    e.""GridRows"", e.""GridCols"", e.""PublishedAt"", e.""ScheduledPublishAt"",
    e.""VenueId"", e.""OrganizerId"",
    e.""CreatedAt"", e.""UpdatedAt"",
    v.""Name"" AS ""VenueName"",
    COALESCE(a.""Line1"", '') AS ""VenueAddress"",
    COALESCE(a.""City"", '') AS ""VenueCity"",
    COALESCE(a.""State"", '') AS ""VenueState"",
    COALESCE(a.""ZipCode"", '') AS ""VenueZipCode"",
    v.""Description"" AS ""VenueDescription"",
    v.""ImagePath"" AS ""VenueImagePath"",
    v.""Phone"" AS ""VenuePhone"",
    v.""Email"" AS ""VenueEmail"",
    v.""Website"" AS ""VenueWebsite"",
    v.""IsActive"" AS ""VenueIsActive"",
    v.""CreatedAt"" AS ""VenueCreatedAt"",
    COALESCE(au.""FirstName"", '') AS ""OrganizerFirstName"",
    COALESCE(au.""LastName"", '') AS ""OrganizerLastName"",
    COALESCE(bs.sold, 0)::int AS ""TotalSold"",
    COALESCE(ts.available, 0)::int AS ""AvailableTables"",
    ts.min_price::int AS ""MinTablePriceCents"",
    ettp.min_price::int AS ""MinTicketTypePriceCents""
FROM events e
JOIN venues v ON e.""VenueId"" = v.""Id""
LEFT JOIN addresses a ON v.""AddressId"" = a.""Id""
LEFT JOIN admin_users au ON e.""OrganizerId"" = au.""Id""
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS sold
    FROM bookings b
    WHERE b.""EventId"" = e.""Id"" AND b.""Status"" IN ('Paid','CheckedIn')
) bs ON true
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS available, MIN(et.""PriceCents"") AS min_price
    FROM tables t
    JOIN event_tables et ON t.""EventTableId"" = et.""Id""
    WHERE t.""EventId"" = e.""Id"" AND t.""IsActive"" = true AND t.""Status"" = 'Available'
) ts ON true
LEFT JOIN LATERAL (
    SELECT MIN(ett.""PriceCents"") AS min_price
    FROM event_ticket_types ett
    WHERE ett.""EventId"" = e.""Id"" AND ett.""IsActive"" = true
) ettp ON true;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW v_event_summary AS
SELECT
    e.""Id"", e.""Title"", e.""Slug"", e.""Status""::text,
    COALESCE(e.""Category""::text, '') AS ""Category"",
    e.""StartDate"", e.""EndDate"", e.""ImagePath"",
    img.""StorageKey"" AS ""PrimaryImageKey"",
    e.""IsFeatured"",
    e.""LayoutMode""::text, e.""PricePerPersonCents"", e.""MaxCapacity"",
    e.""VenueId"",
    v.""Name"" AS ""VenueName"",
    COALESCE(a.""City"", '') AS ""VenueCity"",
    COALESCE(a.""State"", '') AS ""VenueState"",
    e.""OrganizerId"",
    COALESCE(au.""FirstName"" || ' ' || au.""LastName"", '') AS ""OrganizerName"",
    COALESCE(e.""MaxCapacity"", 0)::int AS ""TotalCapacity"",
    COALESCE(bs.sold, 0)::int AS ""TotalSold"",
    COALESCE(ts.available, 0)::int AS ""AvailableTables"",
    ts.min_price::int AS ""MinTablePriceCents"",
    ettp.min_price::int AS ""MinTicketTypePriceCents"",
    e.""CreatedAt""
FROM events e
JOIN venues v ON e.""VenueId"" = v.""Id""
LEFT JOIN addresses a ON v.""AddressId"" = a.""Id""
LEFT JOIN admin_users au ON e.""OrganizerId"" = au.""Id""
LEFT JOIN LATERAL (
    SELECT ""StorageKey""
    FROM images
    WHERE ""EntityType"" = 'event' AND ""EntityId"" = e.""Id"" AND ""IsPrimary"" = true
    LIMIT 1
) img ON true
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS sold
    FROM bookings b
    WHERE b.""EventId"" = e.""Id"" AND b.""Status"" IN ('Paid','CheckedIn')
) bs ON true
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS available, MIN(et.""PriceCents"") AS min_price
    FROM tables t
    JOIN event_tables et ON t.""EventTableId"" = et.""Id""
    WHERE t.""EventId"" = e.""Id"" AND t.""IsActive"" = true AND t.""Status"" = 'Available'
) ts ON true
LEFT JOIN LATERAL (
    SELECT MIN(ett.""PriceCents"") AS min_price
    FROM event_ticket_types ett
    WHERE ett.""EventId"" = e.""Id"" AND ett.""IsActive"" = true
) ettp ON true;
");

            // ─── Step 9: New stored procedures for admin users ───────
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_admin_user(
    p_email text, p_email_hash text, p_first_name text, p_last_name text,
    p_password_hash text, p_role text
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO admin_users (""Email"", ""EmailHash"", ""FirstName"", ""LastName"",
        ""PasswordHash"", ""Role"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
    VALUES (p_email, p_email_hash, p_first_name, p_last_name,
        p_password_hash, p_role, true, now(), now())
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_update_admin_user(
    p_id uuid, p_first_name text DEFAULT NULL, p_last_name text DEFAULT NULL,
    p_phone text DEFAULT NULL, p_role text DEFAULT NULL,
    p_is_active boolean DEFAULT NULL, p_avatar_path text DEFAULT NULL
) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE admin_users SET
        ""FirstName"" = COALESCE(p_first_name, ""FirstName""),
        ""LastName"" = COALESCE(p_last_name, ""LastName""),
        ""Phone"" = COALESCE(p_phone, ""Phone""),
        ""Role"" = COALESCE(p_role, ""Role""),
        ""IsActive"" = COALESCE(p_is_active, ""IsActive""),
        ""AvatarPath"" = COALESCE(p_avatar_path, ""AvatarPath""),
        ""UpdatedAt"" = now()
    WHERE ""Id"" = p_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_update_admin_password(p_id uuid, p_password_hash text)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE admin_users SET ""PasswordHash"" = p_password_hash, ""UpdatedAt"" = now()
    WHERE ""Id"" = p_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_update_admin_last_login(p_id uuid)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE admin_users SET ""LastLoginAt"" = now(), ""UpdatedAt"" = now()
    WHERE ""Id"" = p_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_admin_device_session(
    p_admin_user_id uuid, p_session_hash text, p_fingerprint text,
    p_device_name text, p_ip text, p_expires_at timestamptz
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO device_sessions (""AdminUserId"", ""SessionHash"", ""DeviceFingerprint"",
        ""DeviceName"", ""IpAddress"", ""LastActivityAt"", ""ExpiresAt"", ""CreatedAt"", ""UpdatedAt"")
    VALUES (p_admin_user_id, p_session_hash, p_fingerprint,
        p_device_name, p_ip, now(), p_expires_at, now(), now())
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_revoke_all_admin_sessions(
    p_admin_user_id uuid, p_except_hash text DEFAULT NULL
) RETURNS int LANGUAGE plpgsql AS $$
DECLARE v_count int;
BEGIN
    UPDATE device_sessions SET ""RevokedAt"" = now()
    WHERE ""AdminUserId"" = p_admin_user_id AND ""RevokedAt"" IS NULL
      AND (p_except_hash IS NULL OR ""SessionHash"" <> p_except_hash);
    GET DIAGNOSTICS v_count = ROW_COUNT;
    RETURN v_count;
END; $$;
");

            // ─── Step 10: Update sp_upsert_user to remove role param ─
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_upsert_user(
    p_email text, p_email_hash text, p_first_name text, p_last_name text
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    SELECT ""Id"" INTO v_id FROM users WHERE ""Email"" = p_email;
    IF v_id IS NULL THEN
        INSERT INTO users (""Id"", ""Email"", ""EmailHash"", ""FirstName"", ""LastName"",
            ""IsActive"", ""LastLoginAt"", ""OptInLocationEmail"", ""HasCompletedOnboarding"",
            ""CreatedAt"", ""UpdatedAt"")
        VALUES (gen_random_uuid(), p_email, p_email_hash, p_first_name, p_last_name,
            true, now(), false, false, now(), now())
        RETURNING ""Id"" INTO v_id;
    ELSE
        UPDATE users SET ""LastLoginAt"" = now(), ""UpdatedAt"" = now() WHERE ""Id"" = v_id;
    END IF;
    RETURN v_id;
END; $$;
");

            // ─── Step 11: Drop obsolete sp_update_user_role ──────────
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_update_user_role CASCADE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ─── Restore Role column on users ────────────────────────
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "User");

            // ─── Move admin users back to users ──────────────────────
            migrationBuilder.Sql(@"
INSERT INTO users (""Id"", ""Email"", ""EmailHash"", ""FirstName"", ""LastName"",
    ""Role"", ""IsActive"", ""LastLoginAt"", ""AvatarPath"", ""Phone"",
    ""StripeConnectedAccountId"", ""CreatedAt"", ""UpdatedAt"")
SELECT ""Id"", ""Email"", ""EmailHash"", ""FirstName"", ""LastName"",
    ""Role"", ""IsActive"", ""LastLoginAt"", ""AvatarPath"", ""Phone"",
    ""StripeConnectedAccountId"", ""CreatedAt"", ""UpdatedAt""
FROM admin_users
ON CONFLICT (""Email"") DO NOTHING;
");

            // ─── Restore device_sessions ─────────────────────────────
            migrationBuilder.DropForeignKey(
                name: "FK_device_sessions_admin_users_AdminUserId",
                table: "device_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_device_sessions_UserType",
                table: "device_sessions");

            migrationBuilder.Sql(@"
UPDATE device_sessions SET ""UserId"" = ""AdminUserId""
WHERE ""AdminUserId"" IS NOT NULL AND ""UserId"" IS NULL;
");

            migrationBuilder.DropIndex(
                name: "IX_device_sessions_AdminUserId",
                table: "device_sessions");

            migrationBuilder.DropColumn(
                name: "AdminUserId",
                table: "device_sessions");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "device_sessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // ─── Restore events FK to users ──────────────────────────
            migrationBuilder.DropForeignKey(
                name: "FK_events_admin_users_OrganizerId",
                table: "events");

            migrationBuilder.AddForeignKey(
                name: "FK_events_users_OrganizerId",
                table: "events",
                column: "OrganizerId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ─── Drop admin_users table ──────────────────────────────
            migrationBuilder.DropTable(
                name: "admin_users");

            // ─── Restore check constraint ────────────────────────────
            migrationBuilder.AddCheckConstraint(
                name: "CK_users_Role",
                table: "users",
                sql: "\"Role\" IN ('User','Staff','Admin','Developer')");

            // ─── Restore v_user_profile with Role ────────────────────
            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW v_user_profile AS
SELECT
    u.""Id"", u.""Email"", u.""FirstName"", u.""LastName"",
    u.""Role""::text, u.""IsActive"", u.""LastLoginAt"",
    u.""Phone"", u.""OptInLocationEmail"", u.""HasCompletedOnboarding"",
    u.""AvatarPath"", u.""CreatedAt"",
    a.""Line1"" AS ""AddressLine1"",
    a.""City"", a.""State"", a.""ZipCode""
FROM users u
LEFT JOIN addresses a ON u.""AddressId"" = a.""Id"";
");

            // ─── Restore views with users join ───────────────────────
            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW v_events AS
SELECT
    e.""Id"", e.""Title"", e.""Slug"", e.""Description"", e.""Status""::text,
    COALESCE(e.""Category""::text, '') AS ""Category"",
    e.""StartDate"", e.""EndDate"", e.""ImagePath"", e.""IsFeatured"",
    e.""LayoutMode""::text, e.""MaxCapacity"", e.""PricePerPersonCents"",
    e.""PlatformFeePercent"", e.""PlatformFeeCents"",
    e.""GridRows"", e.""GridCols"", e.""PublishedAt"", e.""ScheduledPublishAt"",
    e.""VenueId"", e.""OrganizerId"",
    e.""CreatedAt"", e.""UpdatedAt"",
    v.""Name"" AS ""VenueName"",
    COALESCE(a.""Line1"", '') AS ""VenueAddress"",
    COALESCE(a.""City"", '') AS ""VenueCity"",
    COALESCE(a.""State"", '') AS ""VenueState"",
    COALESCE(a.""ZipCode"", '') AS ""VenueZipCode"",
    v.""Description"" AS ""VenueDescription"",
    v.""ImagePath"" AS ""VenueImagePath"",
    v.""Phone"" AS ""VenuePhone"",
    v.""Email"" AS ""VenueEmail"",
    v.""Website"" AS ""VenueWebsite"",
    v.""IsActive"" AS ""VenueIsActive"",
    v.""CreatedAt"" AS ""VenueCreatedAt"",
    COALESCE(u.""FirstName"", '') AS ""OrganizerFirstName"",
    COALESCE(u.""LastName"", '') AS ""OrganizerLastName"",
    COALESCE(bs.sold, 0)::int AS ""TotalSold"",
    COALESCE(ts.available, 0)::int AS ""AvailableTables"",
    ts.min_price::int AS ""MinTablePriceCents"",
    ettp.min_price::int AS ""MinTicketTypePriceCents""
FROM events e
JOIN venues v ON e.""VenueId"" = v.""Id""
LEFT JOIN addresses a ON v.""AddressId"" = a.""Id""
LEFT JOIN users u ON e.""OrganizerId"" = u.""Id""
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS sold
    FROM bookings b
    WHERE b.""EventId"" = e.""Id"" AND b.""Status"" IN ('Paid','CheckedIn')
) bs ON true
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS available, MIN(et.""PriceCents"") AS min_price
    FROM tables t
    JOIN event_tables et ON t.""EventTableId"" = et.""Id""
    WHERE t.""EventId"" = e.""Id"" AND t.""IsActive"" = true AND t.""Status"" = 'Available'
) ts ON true
LEFT JOIN LATERAL (
    SELECT MIN(ett.""PriceCents"") AS min_price
    FROM event_ticket_types ett
    WHERE ett.""EventId"" = e.""Id"" AND ett.""IsActive"" = true
) ettp ON true;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW v_event_summary AS
SELECT
    e.""Id"", e.""Title"", e.""Slug"", e.""Status""::text,
    COALESCE(e.""Category""::text, '') AS ""Category"",
    e.""StartDate"", e.""EndDate"", e.""ImagePath"",
    img.""StorageKey"" AS ""PrimaryImageKey"",
    e.""IsFeatured"",
    e.""LayoutMode""::text, e.""PricePerPersonCents"", e.""MaxCapacity"",
    e.""VenueId"",
    v.""Name"" AS ""VenueName"",
    COALESCE(a.""City"", '') AS ""VenueCity"",
    COALESCE(a.""State"", '') AS ""VenueState"",
    e.""OrganizerId"",
    COALESCE(u.""FirstName"" || ' ' || u.""LastName"", '') AS ""OrganizerName"",
    COALESCE(e.""MaxCapacity"", 0)::int AS ""TotalCapacity"",
    COALESCE(bs.sold, 0)::int AS ""TotalSold"",
    COALESCE(ts.available, 0)::int AS ""AvailableTables"",
    ts.min_price::int AS ""MinTablePriceCents"",
    ettp.min_price::int AS ""MinTicketTypePriceCents"",
    e.""CreatedAt""
FROM events e
JOIN venues v ON e.""VenueId"" = v.""Id""
LEFT JOIN addresses a ON v.""AddressId"" = a.""Id""
LEFT JOIN users u ON e.""OrganizerId"" = u.""Id""
LEFT JOIN LATERAL (
    SELECT ""StorageKey""
    FROM images
    WHERE ""EntityType"" = 'event' AND ""EntityId"" = e.""Id"" AND ""IsPrimary"" = true
    LIMIT 1
) img ON true
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS sold
    FROM bookings b
    WHERE b.""EventId"" = e.""Id"" AND b.""Status"" IN ('Paid','CheckedIn')
) bs ON true
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS available, MIN(et.""PriceCents"") AS min_price
    FROM tables t
    JOIN event_tables et ON t.""EventTableId"" = et.""Id""
    WHERE t.""EventId"" = e.""Id"" AND t.""IsActive"" = true AND t.""Status"" = 'Available'
) ts ON true
LEFT JOIN LATERAL (
    SELECT MIN(ett.""PriceCents"") AS min_price
    FROM event_ticket_types ett
    WHERE ett.""EventId"" = e.""Id"" AND ett.""IsActive"" = true
) ettp ON true;
");

            // ─── Restore old SPs ─────────────────────────────────────
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_upsert_user(
    p_email text, p_email_hash text, p_first_name text, p_last_name text, p_role text
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    SELECT ""Id"" INTO v_id FROM users WHERE ""Email"" = p_email;
    IF v_id IS NULL THEN
        INSERT INTO users (""Id"", ""Email"", ""EmailHash"", ""FirstName"", ""LastName"", ""Role"",
            ""IsActive"", ""LastLoginAt"", ""OptInLocationEmail"", ""HasCompletedOnboarding"",
            ""CreatedAt"", ""UpdatedAt"")
        VALUES (gen_random_uuid(), p_email, p_email_hash, p_first_name, p_last_name, p_role,
            true, now(), false, false, now(), now())
        RETURNING ""Id"" INTO v_id;
    ELSE
        UPDATE users SET ""LastLoginAt"" = now(), ""UpdatedAt"" = now() WHERE ""Id"" = v_id;
    END IF;
    RETURN v_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_update_user_role(p_user_id uuid, p_role text)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE users SET ""Role"" = p_role, ""UpdatedAt"" = now() WHERE ""Id"" = p_user_id;
END; $$;
");

            // ─── Drop new admin SPs ──────────────────────────────────
            var adminSps = new[]
            {
                "sp_create_admin_user", "sp_update_admin_user", "sp_update_admin_password",
                "sp_update_admin_last_login", "sp_create_admin_device_session", "sp_revoke_all_admin_sessions"
            };
            foreach (var sp in adminSps)
                migrationBuilder.Sql($"DROP FUNCTION IF EXISTS {sp} CASCADE;");
        }
    }
}
