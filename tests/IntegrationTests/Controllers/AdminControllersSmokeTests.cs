using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Controllers;

/// <summary>
/// Smoke tests for admin/developer/staff controllers: each endpoint gated by [Authorize]+[RequireRole]
/// must return 401 for anonymous and 403 for insufficient role. Happy-path coverage lives in
/// per-controller test files. These smoke tests guarantee AuthZ wiring for every ticket.
/// </summary>
[Collection("Database")]
public sealed class AdminControllersSmokeTests(DatabaseFixture db)
{
    [Theory]
    [InlineData("GET", "/admin/dashboard")]
    [InlineData("GET", "/admin/images")]
    [InlineData("GET", "/admin/logs")]
    [InlineData("GET", "/admin/platform-images")]
    [InlineData("GET", "/admin/purchases")]
    [InlineData("GET", "/admin/staff")]
    [InlineData("GET", "/admin/venues")]
    [InlineData("GET", "/admin/table-templates")]
    [InlineData("GET", "/developer/dashboard")]
    [InlineData("GET", "/developer/logs")]
    [InlineData("GET", "/developer/admin-logs")]
    [InlineData("GET", "/developer/invitations")]
    [InlineData("GET", "/developer/purchases")]
    [InlineData("GET", "/checkin/events")]
    public async Task Endpoint_NoAuth_Returns401(string method, string url)
    {
        var client = db.Factory.CreateClient().WithoutAuth();
        var req = new HttpRequestMessage(new HttpMethod(method), url);
        var resp = await client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/admin/dashboard")]
    [InlineData("/admin/venues")]
    [InlineData("/admin/purchases")]
    [InlineData("/admin/staff")]
    public async Task AdminEndpoint_UserRole_Returns403(string url)
    {
        var client = db.Factory.CreateClient().WithUser();
        var resp = await client.GetAsync(url);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("/developer/dashboard")]
    [InlineData("/developer/logs")]
    [InlineData("/developer/invitations")]
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
        var resp = await client.GetAsync("/checkin/events");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
