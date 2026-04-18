using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class AddStripeTransactionLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_get_stripe_transaction_by_intent(p_intent_id text)
RETURNS SETOF stripe_transactions
LANGUAGE sql STABLE AS $$
    SELECT * FROM stripe_transactions WHERE ""PaymentIntentId"" = p_intent_id;
$$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_stripe_transaction_by_intent(text);");
        }
    }
}
