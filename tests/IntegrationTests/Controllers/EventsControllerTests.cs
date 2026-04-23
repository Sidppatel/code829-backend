using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Contracts.DTOs;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Controllers;

[Collection("Database")]
public sealed class EventsControllerTests(DatabaseFixture db)
{
    [Fact]
    public async Task GetEvents_ReturnsOk_WithPagedResponse()
    {
        var client = db.Factory.CreateClient();
        var resp = await client.GetAsync("/events");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await resp.Content.ReadAsStringAsync();
        json.Should().Contain("items");
    }

    [Fact]
    public async Task GetFacets_ReturnsOk()
    {
        var client = db.Factory.CreateClient();
        var resp = await client.GetAsync("/events/facets");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSchemaList_ReturnsOk()
    {
        var client = db.Factory.CreateClient();
        var resp = await client.GetAsync("/events/schema-list");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404WithApiErrorShape()
    {
        var client = db.Factory.CreateClient();
        var resp = await client.GetAsync($"/events/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await resp.Content.ReadFromJsonAsync<ApiError>(JsonSerializerOptions.Web);
        err.Should().NotBeNull();
        err!.Message.Should().Be("Event not found");
        err.CorrelationId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetBySlug_Unknown_Returns404()
    {
        var client = db.Factory.CreateClient();
        var resp = await client.GetAsync("/events/by-slug/nonexistent-slug");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTables_Unknown_Returns404()
    {
        var client = db.Factory.CreateClient();
        var resp = await client.GetAsync($"/events/{Guid.NewGuid()}/tables");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTicketTypes_Unknown_Returns404()
    {
        var client = db.Factory.CreateClient();
        var resp = await client.GetAsync($"/events/{Guid.NewGuid()}/ticket-types");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
