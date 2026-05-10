using System.Net;
using System.Net.Http.Json;
using Contracts.DTOs;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Controllers;

[Collection("Database")]
public sealed class AdminAuthControllerTests(DatabaseFixture db)
{
    [Fact]
    public async Task Me_NoAuth_Returns401()
    {

        var client = db.Factory.CreateClient().WithoutAuth();
        var resp = await client.GetAsync("/v1/admin/auth/me");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_UserRoleOnly_Returns403()
    {
        var client = db.Factory.CreateClient().WithUser();
        var resp = await client.GetAsync("/v1/admin/auth/me");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var err = await resp.Content.ReadFromJsonAsync<ApiError>();
        err.Should().NotBeNull();
        err!.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Login_EmptyBody_Returns400()
    {
        var client = db.Factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/v1/admin/auth/login", new { email = "", password = "" });
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ForgotPassword_InvalidEmail_ReturnsNoContent()
    {

        var client = db.Factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/v1/admin/auth/forgot-password", new { email = "nope@example.com" });
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Invitation_InvalidToken_Returns404()
    {
        var client = db.Factory.CreateClient();
        var resp = await client.GetAsync("/v1/admin/auth/invitation/bogus-token");
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sessions_NoAuth_Returns401()
    {
        var client = db.Factory.CreateClient().WithoutAuth();
        var resp = await client.GetAsync("/v1/admin/auth/sessions");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
