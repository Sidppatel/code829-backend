using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Controllers;

[Collection("Database")]
public sealed class AdminStripeControllerTests(DatabaseFixture db)
{

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

    [Fact]
    public async Task GetStripeStatus_AdminWithoutOrganization_Returns409()
    {

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

        body.TryGetProperty("stripeAccount", out _).Should().BeFalse();
        body.GetProperty("state").GetString().Should().Be("not_started");
        body.GetProperty("members").GetArrayLength().Should().Be(0);
    }

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

    [Fact]
    public async Task GetStripeStatus_TwoAdminsInSameOrg_BothSeeSameOrgId()
    {

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

    [Fact]
    public async Task GetStripeStatus_AdminInOrgA_DoesNotSeeOrgB()
    {

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
