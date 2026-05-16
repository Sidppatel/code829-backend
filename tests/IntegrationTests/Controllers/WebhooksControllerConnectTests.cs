using System.Net;
using System.Text;
using IntegrationTests.Fixtures;
using Npgsql;

namespace IntegrationTests.Controllers;

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

    [Fact]
    public async Task AccountUpdated_FullyEnabled_FlipsAllFourFlags()
    {
        var stripeAcct = $"acct_test_{Guid.NewGuid():N}".Substring(0, 24);
        var orgId = await TestSeed.SeedOrganizationAsync(db, stripeAccountId: stripeAcct);

        var payload = StripeWebhookSigner.LoadFixture("account_updated.json", stripeAcct, orgId);

        payload = payload.Replace("evt_test_account_updated_001", $"evt_test_account_updated_{Guid.NewGuid():N}");

        var client = db.Factory.CreateClient();
        var resp = await client.SendAsync(BuildSignedRequest(payload));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);

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

        var eventId = $"evt_test_dupe_{Guid.NewGuid():N}";
        payload = payload.Replace("evt_test_account_updated_001", eventId);

        var client = db.Factory.CreateClient();

        var first = await client.SendAsync(BuildSignedRequest(payload));
        var second = await client.SendAsync(BuildSignedRequest(payload));

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);

    }

    [Fact]
    public async Task AccountUpdated_UnknownStripeAccount_Returns200WithoutCrashing()
    {

        var unseededAcct = $"acct_unknown_{Guid.NewGuid():N}".Substring(0, 24);

        var payload = StripeWebhookSigner.LoadFixture("account_updated.json", unseededAcct, Guid.Empty);
        payload = payload.Replace("evt_test_account_updated_001", $"evt_test_unknown_{Guid.NewGuid():N}");

        var client = db.Factory.CreateClient();
        var resp = await client.SendAsync(BuildSignedRequest(payload));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

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

        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM stripe_transfers WHERE \"StripeTransferId\" = @tid";
        cmd.Parameters.AddWithValue("tid", transferId);
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        count.Should().Be(0);
    }

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

        var createdPayload = StripeWebhookSigner.LoadFixture("payout_created.json", stripeAcct, orgId);
        createdPayload = createdPayload.Replace("\"id\": \"po_test_001\"", $"\"id\": \"{payoutId}\"");
        createdPayload = createdPayload.Replace("evt_test_payout_created_001", $"evt_test_pc_{Guid.NewGuid():N}");
        var createdResp = await client.SendAsync(BuildSignedRequest(createdPayload));
        createdResp.StatusCode.Should().Be(HttpStatusCode.OK);

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

        var resp = await client.SendAsync(BuildSignedRequest(payload, signatureOverride: "t=1,v1=deadbeef"));

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

        payload = System.Text.RegularExpressions.Regex.Replace(payload,
            "\"id\":\\s*\"evt_test_[a-z0-9_]+\"",
            $"\"id\": \"evt_test_{Guid.NewGuid():N}\"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var client = db.Factory.CreateClient();
        var resp = await client.SendAsync(BuildSignedRequest(payload));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
