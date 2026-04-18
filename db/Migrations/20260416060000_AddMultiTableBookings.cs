using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTablePurchases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Junction table for multi-table purchases
            migrationBuilder.Sql(@"
CREATE TABLE purchase_tables (
    ""PurchaseId"" uuid NOT NULL REFERENCES purchases(""Id"") ON DELETE CASCADE,
    ""TableId"" uuid NOT NULL REFERENCES tables(""Id"") ON DELETE CASCADE,
    PRIMARY KEY (""PurchaseId"", ""TableId"")
);
CREATE INDEX ""IX_purchase_tables_TableId"" ON purchase_tables (""TableId"");
");

            // Backfill existing single-table purchases
            migrationBuilder.Sql(@"
INSERT INTO purchase_tables (""PurchaseId"", ""TableId"")
SELECT ""Id"", ""TableId"" FROM purchases WHERE ""TableId"" IS NOT NULL
ON CONFLICT DO NOTHING;
");

            // Update sp_create_purchase to also insert into purchase_tables
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_purchase(
    p_user_id uuid, p_event_id uuid, p_table_id uuid, p_seats int,
    p_event_ticket_type_id uuid,
    p_subtotal_cents int, p_fee_cents int, p_total_cents int,
    p_purchase_number text, p_status text DEFAULT 'Pending'
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO purchases (""Id"", ""PurchaseNumber"", ""Status"", ""UserId"", ""EventId"", ""TableId"",
        ""SeatsReserved"", ""EventTicketTypeId"", ""SubtotalCents"", ""FeeCents"", ""TotalCents"",
        ""CreatedAt"", ""UpdatedAt"")
    VALUES (gen_random_uuid(), p_purchase_number, p_status, p_user_id, p_event_id, p_table_id,
        p_seats, p_event_ticket_type_id, p_subtotal_cents, p_fee_cents, p_total_cents,
        now(), now())
    RETURNING ""Id"" INTO v_id;

    IF p_table_id IS NOT NULL THEN
        INSERT INTO purchase_tables (""PurchaseId"", ""TableId"") VALUES (v_id, p_table_id);
    END IF;

    RETURN v_id;
END; $$;
");

            // Update sp_confirm_purchase to mark ALL tables from purchase_tables as booked
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_confirm_purchase(p_purchase_id uuid, p_qr_token text)
RETURNS void LANGUAGE plpgsql AS $$
DECLARE v_seats int; v_seat int;
BEGIN
    UPDATE purchases SET ""Status"" = 'Paid', ""QrToken"" = p_qr_token, ""UpdatedAt"" = now()
    WHERE ""Id"" = p_purchase_id AND ""Status"" = 'Pending'
    RETURNING ""SeatsReserved"" INTO v_seats;

    -- Mark all tables in this purchase as booked
    UPDATE tables SET ""Status"" = 'Booked', ""LockedByUserId"" = NULL,
        ""LockExpiresAt"" = NULL, ""UpdatedAt"" = now()
    WHERE ""Id"" IN (SELECT ""TableId"" FROM purchase_tables WHERE ""PurchaseId"" = p_purchase_id)
      AND ""Status"" IN ('Locked', 'Available');

    v_seats := COALESCE(v_seats, 1);
    FOR v_seat IN 1..v_seats LOOP
        INSERT INTO purchase_tickets (""Id"", ""PurchaseId"", ""TicketCode"", ""QrToken"",
            ""SeatNumber"", ""Status"", ""CreatedAt"", ""UpdatedAt"")
        VALUES (gen_random_uuid(), p_purchase_id,
            'TKT-' || UPPER(SUBSTRING(gen_random_uuid()::text FROM 1 FOR 8)),
            encode(gen_random_bytes(32), 'hex'),
            v_seat, 'Unassigned', now(), now());
    END LOOP;
END; $$;
");

            // Update sp_cancel_purchase to release ALL tables from purchase_tables
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_cancel_purchase(p_purchase_id uuid) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE purchases SET ""Status"" = 'Cancelled', ""UpdatedAt"" = now()
    WHERE ""Id"" = p_purchase_id;

    UPDATE tables SET ""Status"" = 'Available', ""LockedByUserId"" = NULL,
        ""LockExpiresAt"" = NULL, ""UpdatedAt"" = now()
    WHERE ""Id"" IN (SELECT ""TableId"" FROM purchase_tables WHERE ""PurchaseId"" = p_purchase_id)
      AND ""Status"" IN ('Locked', 'Booked');
END; $$;
");

            // Update sp_refund_purchase to release ALL tables from purchase_tables
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_refund_purchase(p_purchase_id uuid) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE purchases SET ""Status"" = 'Refunded', ""UpdatedAt"" = now()
    WHERE ""Id"" = p_purchase_id;
    UPDATE stripe_transactions SET ""Status"" = 'Refunded', ""RefundedAt"" = now(), ""UpdatedAt"" = now()
    WHERE ""PurchaseId"" = p_purchase_id;

    UPDATE tables SET ""Status"" = 'Available', ""LockedByUserId"" = NULL,
        ""LockExpiresAt"" = NULL, ""UpdatedAt"" = now()
    WHERE ""Id"" IN (SELECT ""TableId"" FROM purchase_tables WHERE ""PurchaseId"" = p_purchase_id)
      AND ""Status"" IN ('Locked', 'Booked');
END; $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS purchase_tables;");
        }
    }
}
