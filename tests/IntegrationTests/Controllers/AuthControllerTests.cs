using System.Net;
using System.Net.Http.Json;
using Contracts.DTOs;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Controllers;

[Collection("Database")]
public sealed class AuthControllerTests(DatabaseFixture db)
{
    [Fact]
    public async Task MagicLinkStart_EmptyEmail_Returns400()
    {
        var client = db.Factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/v1/auth/magic-link", new { email = "" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MagicLinkStart_InvalidEmail_Returns400()
    {
        var client = db.Factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/v1/auth/magic-link", new { email = "notanemail" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MagicLinkVerify_InvalidToken_ReturnsErrorStatus()
    {
        var client = db.Factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/v1/auth/magic-link/verify", new { token = "invalid-token" });
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_NoAuth_Returns401()
    {
        var client = db.Factory.CreateClient().WithoutAuth();
        var resp = await client.GetAsync("/v1/auth/me");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_NoAuth_Returns401()
    {
        var client = db.Factory.CreateClient().WithoutAuth();
        var resp = await client.PostAsync("/v1/auth/logout", null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
