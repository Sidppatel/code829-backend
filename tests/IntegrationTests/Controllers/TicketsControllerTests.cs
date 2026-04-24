using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Controllers;

[Collection("Database")]
public sealed class TicketsControllerTests(DatabaseFixture db)
{
    [Fact]
    public async Task GetPurchaseTickets_NoAuth_Returns401()
    {
        var client = db.Factory.CreateClient().WithoutAuth();
        var resp = await client.GetAsync($"/v1/purchases/{Guid.NewGuid()}/tickets");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ClaimGet_NoToken_Returns400Or404()
    {
        var client = db.Factory.CreateClient();
        var resp = await client.GetAsync("/v1/tickets/claim");
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ClaimGet_InvalidToken_Returns400Or404()
    {
        var client = db.Factory.CreateClient();
        var resp = await client.GetAsync("/v1/tickets/claim?token=bogus");
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.Gone);
    }

    [Fact]
    public async Task Mine_NoAuth_Returns401()
    {
        var client = db.Factory.CreateClient().WithoutAuth();
        var resp = await client.GetAsync("/v1/tickets/mine");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TicketQr_NoAuth_Returns401()
    {
        var client = db.Factory.CreateClient().WithoutAuth();
        var resp = await client.GetAsync($"/v1/tickets/{Guid.NewGuid()}/qr");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
