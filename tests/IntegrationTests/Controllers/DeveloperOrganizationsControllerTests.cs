using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Controllers;

[Collection("Database")]
public sealed class DeveloperOrganizationsControllerTests(DatabaseFixture db)
{

    [Fact]
    public async Task ListOrganizations_NoAuth_Returns401()
    {
        var client = db.Factory.CreateClient().WithoutAuth();
        var resp = await client.GetAsync("/v1/developer/organizations");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Staff")]
    [InlineData("User")]
    public async Task ListOrganizations_NonDeveloper_Returns403(string role)
    {
        HttpClient client = db.Factory.CreateClient();
        if (role == "User")
            client = client.WithUser();
        else
            client = client.WithAdmin(role: role);

        var resp = await client.GetAsync("/v1/developer/organizations");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListOrganizations_AsDeveloper_Returns200WithPagination()
    {
        var client = db.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthHelper.GenerateDeveloperJwt());

        var resp = await client.GetAsync("/v1/developer/organizations?page=1&pageSize=5");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
        doc.TryGetProperty("page", out var pageEl).Should().BeTrue();
        pageEl.GetInt32().Should().Be(1);
        doc.TryGetProperty("pageSize", out var sizeEl).Should().BeTrue();
        sizeEl.GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task CreateOrganization_AsDeveloper_Returns201_WithBody()
    {
        var client = db.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthHelper.GenerateDeveloperJwt());

        var resp = await client.PostAsJsonAsync("/v1/developer/organizations", new
        {
            name = $"TestOrg-{Guid.NewGuid():N}",
            legalName = "Test Org LLC",
            countryCode = "US"
        });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("id", out _).Should().BeTrue();
    }

    [Fact]
    public async Task CreateOrganization_WithInitialMember_AttachesMember()
    {
        var bu = await TestSeed.SeedBusinessUserWithOrgAsync(db);

        var client = db.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthHelper.GenerateDeveloperJwt());

        var resp = await client.PostAsJsonAsync("/v1/developer/organizations", new
        {
            name = $"TestOrgWithMember-{Guid.NewGuid():N}",
            countryCode = "US",
            initialMemberBusinessUserId = bu
        });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateOrganization_WithNonexistentMember_Returns400()
    {
        var client = db.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthHelper.GenerateDeveloperJwt());

        var resp = await client.PostAsJsonAsync("/v1/developer/organizations", new
        {
            name = "BadMember",
            countryCode = "US",
            initialMemberBusinessUserId = Guid.NewGuid()
        });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetOrganization_Existing_Returns200()
    {
        var orgId = await TestSeed.SeedOrganizationAsync(db);

        var client = db.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthHelper.GenerateDeveloperJwt());

        var resp = await client.GetAsync($"/v1/developer/organizations/{orgId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetGuid().Should().Be(orgId);
    }

    [Fact]
    public async Task GetOrganization_Missing_Returns404()
    {
        var client = db.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthHelper.GenerateDeveloperJwt());

        var resp = await client.GetAsync($"/v1/developer/organizations/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateOrganization_Existing_Returns200()
    {
        var orgId = await TestSeed.SeedOrganizationAsync(db);

        var client = db.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthHelper.GenerateDeveloperJwt());

        var resp = await client.PutAsJsonAsync($"/v1/developer/organizations/{orgId}", new
        {
            name = $"Renamed-{Guid.NewGuid():N}",
            countryCode = "GB"
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AddMember_OrgNotFound_Returns404()
    {
        var client = db.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthHelper.GenerateDeveloperJwt());

        var resp = await client.PostAsJsonAsync(
            $"/v1/developer/organizations/{Guid.NewGuid()}/members",
            new { businessUserId = Guid.NewGuid() });
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddMember_BusinessUserMissing_Returns404()
    {
        var orgId = await TestSeed.SeedOrganizationAsync(db);

        var client = db.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthHelper.GenerateDeveloperJwt());

        var resp = await client.PostAsJsonAsync(
            $"/v1/developer/organizations/{orgId}/members",
            new { businessUserId = Guid.NewGuid() });
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddMember_HappyPath_Returns200()
    {
        var orgId = await TestSeed.SeedOrganizationAsync(db);
        var bu = await TestSeed.SeedBusinessUserWithOrgAsync(db);

        var client = db.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthHelper.GenerateDeveloperJwt());

        var resp = await client.PostAsJsonAsync(
            $"/v1/developer/organizations/{orgId}/members",
            new { businessUserId = bu });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RemoveMember_OrgNotFound_Returns404()
    {
        var client = db.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthHelper.GenerateDeveloperJwt());

        var resp = await client.DeleteAsync(
            $"/v1/developer/organizations/{Guid.NewGuid()}/members/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateStripeAccount_OrgNotFound_Returns404()
    {
        var client = db.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthHelper.GenerateDeveloperJwt());

        var resp = await client.PostAsync(
            $"/v1/developer/organizations/{Guid.NewGuid()}/stripe-account", null);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateStripeOnboardingLink_NoStripeAccount_Returns400()
    {
        var orgId = await TestSeed.SeedOrganizationAsync(db);

        var client = db.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthHelper.GenerateDeveloperJwt());

        var resp = await client.PostAsJsonAsync(
            $"/v1/developer/organizations/{orgId}/stripe-onboarding-link",
            new { scope = "identity" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetStripeStatus_NoStripeAccount_Returns200WithEmptyShell()
    {

        var orgId = await TestSeed.SeedOrganizationAsync(db);

        var client = db.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthHelper.GenerateDeveloperJwt());

        var resp = await client.GetAsync(
            $"/v1/developer/organizations/{orgId}/stripe-status");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("organizationId").GetGuid().Should().Be(orgId);
        body.GetProperty("state").GetString().Should().Be("not_started");

        var stripeAccountPresent = body.TryGetProperty("stripeAccount", out var sa)
            && sa.ValueKind != JsonValueKind.Null;
        stripeAccountPresent.Should().BeFalse();
    }

    [Fact]
    public async Task SendStripeOnboardingEmail_OrgNotFound_Returns404()
    {
        var client = db.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthHelper.GenerateDeveloperJwt());

        var resp = await client.PostAsJsonAsync(
            $"/v1/developer/organizations/{Guid.NewGuid()}/stripe-onboarding-email",
            new { businessUserId = Guid.NewGuid() });
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
