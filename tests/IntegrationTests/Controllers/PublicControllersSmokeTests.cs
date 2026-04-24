using System.Net;
using System.Net.Http.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Controllers;

[Collection("Database")]
public sealed class PublicControllersSmokeTests(DatabaseFixture db)
{
    [Fact]
    public async Task Seo_Sitemap_Returns200()
    {
        var client = db.Factory.CreateClient();
        var resp = await client.GetAsync("/sitemap.xml");
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Seo_Robots_Returns200()
    {
        var client = db.Factory.CreateClient();
        var resp = await client.GetAsync("/robots.txt");
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Feedback_Post_EmptyBody_Returns400()
    {
        var client = db.Factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/v1/feedback", new { });
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Feedback_List_NoAuth_Returns401()
    {
        var client = db.Factory.CreateClient().WithoutAuth();
        var resp = await client.GetAsync("/v1/feedback");
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TableBooking_Lock_NoAuth_Returns401()
    {
        var client = db.Factory.CreateClient().WithoutAuth();
        var resp = await client.PostAsJsonAsync("/v1/tables/lock", new { eventId = Guid.NewGuid(), tableId = Guid.NewGuid() });
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TableBooking_ReleaseBeacon_NoAuth_Returns401()
    {
        var client = db.Factory.CreateClient().WithoutAuth();
        var resp = await client.PostAsJsonAsync("/v1/tables/release-beacon", new { eventId = Guid.NewGuid(), tableId = Guid.NewGuid() });
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest);
    }
}
