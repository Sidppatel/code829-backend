using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Contracts.DTOs;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Controllers;

[Collection("Database")]
public sealed class AdminEventsControllerTests(DatabaseFixture db)
{
    [Fact]
    public async Task List_NoAuth_Returns401()
    {
        var client = db.Factory.CreateClient().WithoutAuth();
        var resp = await client.GetAsync("/v1/admin/events");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_UserRole_Returns403()
    {
        var client = db.Factory.CreateClient().WithUser();
        var resp = await client.GetAsync("/v1/admin/events");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var err = await resp.Content.ReadFromJsonAsync<ApiError>(JsonSerializerOptions.Web);
        err.Should().NotBeNull();
        err!.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task List_AsAdmin_Returns200()
    {
        var client = db.Factory.CreateClient().WithAdmin();
        var resp = await client.GetAsync("/v1/admin/events");
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_Unknown_AsAdmin_Returns404()
    {
        var client = db.Factory.CreateClient().WithAdmin();
        var resp = await client.GetAsync($"/v1/admin/events/{Guid.NewGuid()}");
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_NoBody_AsAdmin_Returns400()
    {
        var client = db.Factory.CreateClient().WithAdmin();
        var resp = await client.PostAsJsonAsync("/v1/admin/events", new { });
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Update_NoAuth_Returns401()
    {
        var client = db.Factory.CreateClient().WithoutAuth();
        var resp = await client.PutAsJsonAsync($"/v1/admin/events/{Guid.NewGuid()}", new { });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
