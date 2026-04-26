using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Adds TableLabels (text[]) to v_purchases so multi-table purchases can render
    /// every booked table label, not just the primary one stored on purchases.TableId.
    /// </remarks>
    public partial class AddTableLabelsToPurchaseView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(MigrationSqlLoader.Load("v_purchases.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW v_purchases AS
SELECT
    b.""Id"" AS ""PurchaseId"", b.""PurchaseNumber"", b.""Status""::text,
    b.""SubtotalCents"", b.""FeeCents"", b.""TotalCents"",
    b.""QrToken"", b.""SeatsReserved"", b.""CreatedAt"",
    b.""UserId"",
    u.""Email"" AS ""UserEmail"",
    u.""FirstName"" AS ""UserFirstName"",
    u.""LastName"" AS ""UserLastName"",
    b.""EventId"",
    e.""Title"" AS ""EventTitle"",
    e.""Slug"" AS ""EventSlug"",
    e.""StartDate"" AS ""EventStartDate"",
    e.""EndDate"" AS ""EventEndDate"",
    COALESCE(e.""Category""::text, '') AS ""EventCategory"",
    e.""ImagePath"" AS ""EventImagePath"",
    v.""Name"" AS ""VenueName"",
    COALESCE(addr.""Line1"", '') AS ""VenueAddress"",
    COALESCE(addr.""City"", '') AS ""VenueCity"",
    COALESCE(addr.""State"", '') AS ""VenueState"",
    b.""TableId"",
    tbl.""Label"" AS ""TableLabel"",
    b.""EventTicketTypeId"",
    ett.""Label"" AS ""EventTicketTypeLabel"",
    st.""Id"" AS ""StripeTransactionId"",
    st.""PaymentIntentId"",
    st.""TaxCalculationId"",
    st.""TaxTransactionId"",
    st.""Status""::text AS ""PaymentStatus"",
    st.""AmountCents"" AS ""PaymentAmountCents"",
    st.""TotalChargedCents"",
    st.""TaxAmountCents"",
    st.""StripeFeesCents"",
    st.""TransferAmountCents"",
    st.""PaidAt"", st.""RefundedAt"",
    COALESCE(tc.cnt, 0)::int AS ""TicketCount"",
    e.""BusinessUserId""
FROM purchases b
JOIN users u ON b.""UserId"" = u.""Id""
JOIN events e ON b.""EventId"" = e.""Id""
JOIN venues v ON e.""VenueId"" = v.""Id""
LEFT JOIN addresses addr ON v.""AddressId"" = addr.""Id""
LEFT JOIN tables tbl ON b.""TableId"" = tbl.""Id""
LEFT JOIN event_ticket_types ett ON b.""EventTicketTypeId"" = ett.""Id""
LEFT JOIN stripe_transactions st ON st.""PurchaseId"" = b.""Id""
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS cnt FROM purchase_tickets bt WHERE bt.""PurchaseId"" = b.""Id""
) tc ON true;
");
        }
    }
}
