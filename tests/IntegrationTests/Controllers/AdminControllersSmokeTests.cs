using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Controllers;

[Collection("Database")]
public sealed class AdminControllersSmokeTests(DatabaseFixture db)
{
    [Theory]
    [InlineData("GET", "/v1/admin/dashboard")]
    [InlineData("GET", "/v1/admin/images")]
    [InlineData("GET", "/v1/admin/logs")]
    [InlineData("GET", "/v1/admin/platform-images")]
    [InlineData("GET", "/v1/admin/purchases")]
    [InlineData("GET", "/v1/admin/staff")]
    [InlineData("GET", "/v1/admin/venues")]
    [InlineData("GET", "/v1/admin/table-templates")]
    [InlineData("GET", "/v1/developer/dashboard")]
    [InlineData("GET", "/v1/developer/logs")]
    [InlineData("GET", "/v1/developer/admin-logs")]
    [InlineData("GET", "/v1/developer/invitations")]
    [InlineData("GET", "/v1/developer/purchases")]
    [InlineData("GET", "/v1/developer/visits/stats")]
    [InlineData("GET", "/v1/checkin/events")]
    public async Task Endpoint_NoAuth_Returns401(string method, string url)
    {
        var client = db.Factory.CreateClient().WithoutAuth();
        var req = new HttpRequestMessage(new HttpMethod(method), url);
        var resp = await client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/v1/admin/dashboard")]
    [InlineData("/v1/admin/venues")]
    [InlineData("/v1/admin/purchases")]
    [InlineData("/v1/admin/staff")]
    public async Task AdminEndpoint_UserRole_Returns403(string url)
    {
        var client = db.Factory.CreateClient().WithUser();
        var resp = await client.GetAsync(url);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("/v1/developer/dashboard")]
    [InlineData("/v1/developer/logs")]
    [InlineData("/v1/developer/invitations")]
    public async Task DeveloperEndpoint_AdminRole_Returns403(string url)
    {
        var client = db.Factory.CreateClient().WithAdmin();
        var resp = await client.GetAsync(url);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CheckIn_UserRole_Returns403()
    {
        var client = db.Factory.CreateClient().WithUser();
        var resp = await client.GetAsync("/v1/checkin/events");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
