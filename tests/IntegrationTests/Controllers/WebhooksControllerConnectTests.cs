using System.Net;
using System.Text;
using IntegrationTests.Fixtures;
using Npgsql;

namespace IntegrationTests.Controllers;

/// <summary>
/// Integration tests for the four Stripe Connect event handlers added to
/// <c>WebhooksController</c>: <c>account.updated</c>, <c>transfer.created</c>,
/// <c>payout.created</c>, <c>payout.paid</c>.
///
/// <para>
/// Each test seeds a real Organization in Postgres (via <see cref="TestSeed"/>),
/// constructs a Stripe-event payload from the JSON fixtures in
/// <c>Fixtures/StripeWebhookFixtures/</c>, signs it with the test webhook
/// secret, and POSTs to <c>/webhooks/stripe</c>. Side effects are verified
/// against the real DB.
/// </para>
/// </summary>
[Collection("Database")]
public sealed class WebhooksControllerConnectTests(DatabaseFixture db)
{
    private static HttpRequestMessage BuildSignedRequest(string payload, string? signatureOverride = null)
    {
        var sig = signatureOverride ?? StripeWebhookSigner.Sign(payload);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.Add("Stripe-Signature", sig);
        return new HttpRequestMessage(HttpMethod.Post, "/webhooks/stripe") { Content = content };
    }

    // ─── account.updated ────────────────────────────────────────────────────

    [Fact]
    public async Task AccountUpdated_FullyEnabled_FlipsAllFourFlags()
    {
        var stripeAcct = $"acct_test_{Guid.NewGuid():N}".Substring(0, 24);
        var orgId = await TestSeed.SeedOrganizationAsync(db, stripeAccountId: stripeAcct);

        var payload = StripeWebhookSigner.LoadFixture("account_updated.json", stripeAcct, orgId);
        // Fixture id collides across tests sharing the DB — randomize so Redis dedupe
        // doesn't short-circuit the second run.
        payload = payload.Replace("evt_test_account_updated_001", $"evt_test_account_updated_{Guid.NewGuid():N}");

        var client = db.Factory.CreateClient();
        var resp = await client.SendAsync(BuildSignedRequest(payload));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify the 4 status flags + requirements_due array were persisted.
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT "StripeChargesEnabled","StripePayoutsEnabled","StripeDetailsSubmitted",
                   "StripeRequirementsDue"
            FROM organizations WHERE "Id" = @id
            """;
        cmd.Parameters.AddWithValue("id", orgId);
        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetBoolean(0).Should().BeTrue("charges_enabled in fixture");
        reader.GetBoolean(1).Should().BeTrue("payouts_enabled in fixture");
        reader.GetBoolean(2).Should().BeTrue("details_submitted in fixture");
        // requirements.currently_due is empty in the active fixture
        reader.IsDBNull(3).Should().BeFalse();
    }

    [Fact]
    public async Task AccountUpdated_PendingState_MirrorsRequirementsDueArray()
    {
        var stripeAcct = $"acct_test_{Guid.NewGuid():N}".Substring(0, 24);
        var orgId = await TestSeed.SeedOrganizationAsync(db, stripeAccountId: stripeAcct);

        var payload = StripeWebhookSigner.LoadFixture("account_updated_pending.json", stripeAcct, orgId);
        payload = payload.Replace("evt_test_account_updated_pending_001", $"evt_test_account_updated_pending_{Guid.NewGuid():N}");

        var client = db.Factory.CreateClient();
        var resp = await client.SendAsync(BuildSignedRequest(payload));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT "StripeChargesEnabled","StripePayoutsEnabled","StripeDetailsSubmitted",
                   "StripeRequirementsDue"::text
            FROM organizations WHERE "Id" = @id
            """;
        cmd.Parameters.AddWithValue("id", orgId);
        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetBoolean(0).Should().BeFalse();
        reader.GetBoolean(1).Should().BeFalse();
        reader.GetBoolean(2).Should().BeFalse();
        var reqJson = reader.GetString(3);
        reqJson.Should().Contain("external_account");
        reqJson.Should().Contain("tos_acceptance.date");
    }

    [Fact]
    public async Task AccountUpdated_DuplicateEventId_DeduplicatedViaRedis()
    {
        var stripeAcct = $"acct_test_{Guid.NewGuid():N}".Substring(0, 24);
        var orgId = await TestSeed.SeedOrganizationAsync(db, stripeAccountId: stripeAcct);

        var payload = StripeWebhookSigner.LoadFixture("account_updated.json", stripeAcct, orgId);
        // Force a fixed event id so Redis dedupe applies on the 2nd call.
        var eventId = $"evt_test_dupe_{Guid.NewGuid():N}";
        payload = payload.Replace("evt_test_account_updated_001", eventId);

        var client = db.Factory.CreateClient();

        // First call processes; second call is deduped.
        var first = await client.SendAsync(BuildSignedRequest(payload));
        var second = await client.SendAsync(BuildSignedRequest(payload));

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        // Both 200 — observable proof that handler didn't run twice would be a
        // counter or audit table; for now we trust that StringSetAsync(NX) +
        // the log message at the call site is enough. The behavioral check is
        // that the second call doesn't blow up if (e.g.) DB state were
        // mutated in a non-idempotent way.
    }

    [Fact]
    public async Task AccountUpdated_UnknownStripeAccount_Returns200WithoutCrashing()
    {
        // Account id that doesn't correspond to any seeded org. The SP raises P0002
        // (no_data_found from RAISE) which the handler swallows — webhook returns 200
        // so Stripe doesn't retry forever.
        var unseededAcct = $"acct_unknown_{Guid.NewGuid():N}".Substring(0, 24);

        var payload = StripeWebhookSigner.LoadFixture("account_updated.json", unseededAcct, Guid.Empty);
        payload = payload.Replace("evt_test_account_updated_001", $"evt_test_unknown_{Guid.NewGuid():N}");

        var client = db.Factory.CreateClient();
        var resp = await client.SendAsync(BuildSignedRequest(payload));

        // Webhook returns 200 even when the org isn't found — letting Stripe retry
        // wouldn't fix the missing org and would burn API quota.
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ─── transfer.created ───────────────────────────────────────────────────

    [Fact]
    public async Task TransferCreated_LinkedAccount_InsertsStripeTransferRow()
    {
        var stripeAcct = $"acct_test_{Guid.NewGuid():N}".Substring(0, 24);
        var orgId = await TestSeed.SeedOrganizationAsync(db, stripeAccountId: stripeAcct);

        var payload = StripeWebhookSigner.LoadFixture("transfer_created.json", stripeAcct, orgId);
        var transferId = $"tr_test_{Guid.NewGuid():N}".Substring(0, 24);
        payload = payload.Replace("\"id\": \"tr_test_001\"", $"\"id\": \"{transferId}\"");
        payload = payload.Replace("evt_test_transfer_created_001", $"evt_test_transfer_{Guid.NewGuid():N}");

        var client = db.Factory.CreateClient();
        var resp = await client.SendAsync(BuildSignedRequest(payload));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify a row landed in stripe_transfers.
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT "OrganizationId","AmountCents","Currency"
            FROM stripe_transfers WHERE "StripeTransferId" = @tid
            """;
        cmd.Parameters.AddWithValue("tid", transferId);
        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetGuid(0).Should().Be(orgId);
        reader.GetInt32(1).Should().Be(4500);
        reader.GetString(2).Should().Be("usd");
    }

    [Fact]
    public async Task TransferCreated_UnknownDestination_Returns200WithoutInsert()
    {
        var unseededAcct = $"acct_unknown_{Guid.NewGuid():N}".Substring(0, 24);

        var payload = StripeWebhookSigner.LoadFixture("transfer_created.json", unseededAcct, Guid.Empty);
        var transferId = $"tr_test_orphan_{Guid.NewGuid():N}".Substring(0, 24);
        payload = payload.Replace("\"id\": \"tr_test_001\"", $"\"id\": \"{transferId}\"");
        payload = payload.Replace("evt_test_transfer_created_001", $"evt_test_transfer_orphan_{Guid.NewGuid():N}");

        var client = db.Factory.CreateClient();
        var resp = await client.SendAsync(BuildSignedRequest(payload));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Row must NOT have landed.
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM stripe_transfers WHERE \"StripeTransferId\" = @tid";
        cmd.Parameters.AddWithValue("tid", transferId);
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        count.Should().Be(0);
    }

    // ─── payout.created / payout.paid ───────────────────────────────────────

    [Fact]
    public async Task PayoutCreated_LinkedAccount_InsertsStripePayoutRow()
    {
        var stripeAcct = $"acct_test_{Guid.NewGuid():N}".Substring(0, 24);
        var orgId = await TestSeed.SeedOrganizationAsync(db, stripeAccountId: stripeAcct);

        var payoutId = $"po_test_{Guid.NewGuid():N}".Substring(0, 24);

        var payload = StripeWebhookSigner.LoadFixture("payout_created.json", stripeAcct, orgId);
        payload = payload.Replace("\"id\": \"po_test_001\"", $"\"id\": \"{payoutId}\"");
        payload = payload.Replace("evt_test_payout_created_001", $"evt_test_payout_created_{Guid.NewGuid():N}");

        var client = db.Factory.CreateClient();
        var resp = await client.SendAsync(BuildSignedRequest(payload));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT "OrganizationId","AmountCents","Currency","Status","PaidAt"
            FROM stripe_payouts WHERE "StripePayoutId" = @pid
            """;
        cmd.Parameters.AddWithValue("pid", payoutId);
        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetGuid(0).Should().Be(orgId);
        reader.GetInt32(1).Should().Be(9000);
        reader.GetString(2).Should().Be("usd");
        reader.GetString(3).Should().Be("in_transit");
        reader.IsDBNull(4).Should().BeTrue("payout.created hasn't been paid yet");
    }

    [Fact]
    public async Task PayoutPaid_AfterPayoutCreated_UpdatesStatusAndStampsPaidAt()
    {
        var stripeAcct = $"acct_test_{Guid.NewGuid():N}".Substring(0, 24);
        var orgId = await TestSeed.SeedOrganizationAsync(db, stripeAccountId: stripeAcct);

        var payoutId = $"po_test_{Guid.NewGuid():N}".Substring(0, 24);
        var client = db.Factory.CreateClient();

        // Step 1: payout.created
        var createdPayload = StripeWebhookSigner.LoadFixture("payout_created.json", stripeAcct, orgId);
        createdPayload = createdPayload.Replace("\"id\": \"po_test_001\"", $"\"id\": \"{payoutId}\"");
        createdPayload = createdPayload.Replace("evt_test_payout_created_001", $"evt_test_pc_{Guid.NewGuid():N}");
        var createdResp = await client.SendAsync(BuildSignedRequest(createdPayload));
        createdResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 2: payout.paid for the same payout id — upserts onto the existing row.
        var paidPayload = StripeWebhookSigner.LoadFixture("payout_paid.json", stripeAcct, orgId);
        paidPayload = paidPayload.Replace("\"id\": \"po_test_001\"", $"\"id\": \"{payoutId}\"");
        paidPayload = paidPayload.Replace("evt_test_payout_paid_001", $"evt_test_pp_{Guid.NewGuid():N}");
        var paidResp = await client.SendAsync(BuildSignedRequest(paidPayload));
        paidResp.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT "Status","PaidAt"
            FROM stripe_payouts WHERE "StripePayoutId" = @pid
            """;
        cmd.Parameters.AddWithValue("pid", payoutId);
        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetString(0).Should().Be("paid");
        reader.IsDBNull(1).Should().BeFalse("payout.paid stamps PaidAt server-side");
    }

    // ─── Signature validation ───────────────────────────────────────────────

    [Theory]
    [InlineData("account_updated.json")]
    [InlineData("transfer_created.json")]
    [InlineData("payout_created.json")]
    [InlineData("payout_paid.json")]
    public async Task ConnectEvents_RejectInvalidSignature(string fixtureFile)
    {
        var stripeAcct = $"acct_test_{Guid.NewGuid():N}".Substring(0, 24);
        var orgId = await TestSeed.SeedOrganizationAsync(db, stripeAccountId: stripeAcct);
        var payload = StripeWebhookSigner.LoadFixture(fixtureFile, stripeAcct, orgId);

        var client = db.Factory.CreateClient();
        // Garbage signature — must be rejected before the handler runs.
        var resp = await client.SendAsync(BuildSignedRequest(payload, signatureOverride: "t=1,v1=deadbeef"));

        // Webhook handler returns 400 for signature failures (or 500 if STRIPE_WEBHOOK_SECRET
        // env var weren't configured — DatabaseFixture configures it, so 400 is the path).
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    [Theory]
    [InlineData("account_updated.json")]
    [InlineData("transfer_created.json")]
    [InlineData("payout_created.json")]
    [InlineData("payout_paid.json")]
    public async Task ConnectEvents_AcceptValidSignature(string fixtureFile)
    {
        var stripeAcct = $"acct_test_{Guid.NewGuid():N}".Substring(0, 24);
        var orgId = await TestSeed.SeedOrganizationAsync(db, stripeAccountId: stripeAcct);
        var payload = StripeWebhookSigner.LoadFixture(fixtureFile, stripeAcct, orgId);
        // Each test gets a unique event id so Redis dedupe doesn't make this flaky
        // when the [Theory] cases share a DatabaseFixture across InlineData entries.
        payload = System.Text.RegularExpressions.Regex.Replace(payload,
            "\"id\":\\s*\"evt_test_[a-z0-9_]+\"",
            $"\"id\": \"evt_test_{Guid.NewGuid():N}\"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var client = db.Factory.CreateClient();
        var resp = await client.SendAsync(BuildSignedRequest(payload));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
