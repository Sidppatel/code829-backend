using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Controllers;

/// <summary>
/// Integration tests for <c>/v1/admin/organization/stripe-*</c> endpoints. These
/// resolve the org from the authenticated admin's <c>BusinessUser.OrganizationId</c>
/// claim — i.e. there's no <c>{orgId}</c> in the route, so admins can't probe other
/// orgs. The tests verify that ownership boundary is enforced.
/// </summary>
[Collection("Database")]
public sealed class AdminStripeControllerTests(DatabaseFixture db)
{
    // ─── Auth + role gating ─────────────────────────────────────────────────

    [Fact]
    public async Task GetStripeStatus_NoAuth_Returns401()
    {
        var client = db.Factory.CreateClient().WithoutAuth();
        var resp = await client.GetAsync("/v1/admin/organization/stripe-status");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("User")]
    [InlineData("Staff")]
    public async Task GetStripeStatus_NonAdmin_Returns403(string role)
    {
        HttpClient client = db.Factory.CreateClient();
        if (role == "User") client = client.WithUser();
        else client = client.WithAdmin(role: role);

        var resp = await client.GetAsync("/v1/admin/organization/stripe-status");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ─── Admin without an org ───────────────────────────────────────────────

    [Fact]
    public async Task GetStripeStatus_AdminWithoutOrganization_Returns409()
    {
        // Admin whose BusinessUser row has no OrganizationId. ResolveOwningOrgAsync
        // returns 409 Conflict telling them to contact a developer.
        var bu = await TestSeed.SeedBusinessUserWithOrgAsync(db, organizationId: null);

        var client = db.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                AuthHelper.GenerateAdminJwt(bu));

        var resp = await client.GetAsync("/v1/admin/organization/stripe-status");
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetStripeStatus_OrgWithoutStripeAccount_Returns200WithEmptyShell()
    {
        // Org exists but no Stripe account yet — controller returns 200 with
        // null stripeAccountId so the FE can render the "ask developer" empty state.
        var orgId = await TestSeed.SeedOrganizationAsync(db, stripeAccountId: null);
        var bu = await TestSeed.SeedBusinessUserWithOrgAsync(db, organizationId: orgId);

        var client = db.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                AuthHelper.GenerateAdminJwt(bu));

        var resp = await client.GetAsync("/v1/admin/organization/stripe-status");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("organizationId").GetGuid().Should().Be(orgId);
        // Rich DTO: stripeAccount is null (elided by WhenWritingNull); state
        // derived as "not_started" via OrganizationStripeStateMapper.
        body.TryGetProperty("stripeAccount", out _).Should().BeFalse();
        body.GetProperty("state").GetString().Should().Be("not_started");
        body.GetProperty("members").GetArrayLength().Should().Be(0);
    }

    // ─── Resume link ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateResumeLink_AdminWithoutOrg_Returns409()
    {
        var bu = await TestSeed.SeedBusinessUserWithOrgAsync(db, organizationId: null);

        var client = db.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                AuthHelper.GenerateAdminJwt(bu));

        var resp = await client.PostAsJsonAsync("/v1/admin/organization/stripe-resume-link",
            new { scope = "identity" });
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateResumeLink_OrgWithoutStripeAccount_Returns409()
    {
        // Org exists but has no Stripe account — controller returns 409 telling
        // the admin to contact a developer (rather than 400, which would imply
        // a request issue).
        var orgId = await TestSeed.SeedOrganizationAsync(db, stripeAccountId: null);
        var bu = await TestSeed.SeedBusinessUserWithOrgAsync(db, organizationId: orgId);

        var client = db.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                AuthHelper.GenerateAdminJwt(bu));

        var resp = await client.PostAsJsonAsync("/v1/admin/organization/stripe-resume-link",
            new { scope = "identity" });
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ─── Cross-admin in same org sees same status ───────────────────────────

    [Fact]
    public async Task GetStripeStatus_TwoAdminsInSameOrg_BothSeeSameOrgId()
    {
        // The "shared payout account" rule: every admin in an organization sees
        // the same Stripe state. Verifies that resolver returns the org regardless
        // of which admin is authenticated.
        var orgId = await TestSeed.SeedOrganizationAsync(db, stripeAccountId: null);
        var adminA = await TestSeed.SeedBusinessUserWithOrgAsync(db, organizationId: orgId);
        var adminB = await TestSeed.SeedBusinessUserWithOrgAsync(db, organizationId: orgId);

        async Task<Guid> CallAsAsync(Guid buId)
        {
            var client = db.Factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                    AuthHelper.GenerateAdminJwt(buId));
            var resp = await client.GetAsync("/v1/admin/organization/stripe-status");
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
            return body.GetProperty("organizationId").GetGuid();
        }

        var seenByA = await CallAsAsync(adminA);
        var seenByB = await CallAsAsync(adminB);

        seenByA.Should().Be(orgId);
        seenByB.Should().Be(orgId);
        seenByA.Should().Be(seenByB);
    }

    // ─── Cross-org isolation ────────────────────────────────────────────────

    [Fact]
    public async Task GetStripeStatus_AdminInOrgA_DoesNotSeeOrgB()
    {
        // Two orgs, two admins (one in each). Admin A's response must reference
        // org A only. There's no {orgId} on the route so this is implicit, but
        // we still verify by checking the returned organizationId.
        var orgA = await TestSeed.SeedOrganizationAsync(db);
        var orgB = await TestSeed.SeedOrganizationAsync(db);
        var adminA = await TestSeed.SeedBusinessUserWithOrgAsync(db, organizationId: orgA);

        var client = db.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                AuthHelper.GenerateAdminJwt(adminA));

        var resp = await client.GetAsync("/v1/admin/organization/stripe-status");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("organizationId").GetGuid().Should().Be(orgA);
        body.GetProperty("organizationId").GetGuid().Should().NotBe(orgB);
    }
}
