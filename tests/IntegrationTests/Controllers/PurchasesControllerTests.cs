using System.Net;
using System.Net.Http.Json;
using Contracts.DTOs;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Controllers;

[Collection("Database")]
public sealed class PurchasesControllerTests(DatabaseFixture db)
{
    [Fact]
    public async Task Create_NoAuth_Returns401()
    {
        var client = db.Factory.CreateClient().WithoutAuth();
        var resp = await client.PostAsJsonAsync("/v1/purchases", new { eventId = Guid.NewGuid(), seats = 1 });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Quote_NoAuth_AllowsAnonymousAndReturns404ForUnknownEvent()
    {

        var client = db.Factory.CreateClient().WithoutAuth();
        var resp = await client.PostAsJsonAsync("/v1/purchases/quote",
            new { eventId = Guid.NewGuid(), tableIds = new[] { Guid.NewGuid() } });
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CheckoutQuote_NoAuth_Returns401()
    {
        var client = db.Factory.CreateClient().WithoutAuth();
        var resp = await client.PostAsJsonAsync("/v1/purchases/checkout-quote",
            new { eventId = Guid.NewGuid(), seatCount = 1 });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Mine_NoAuth_Returns401()
    {
        var client = db.Factory.CreateClient().WithoutAuth();
        var resp = await client.GetAsync("/v1/purchases/mine");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_UnknownAsUser_Returns404()
    {
        var client = db.Factory.CreateClient().WithUser();
        var resp = await client.GetAsync($"/v1/purchases/{Guid.NewGuid()}");
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Refund_UserRole_Returns403()
    {
        var client = db.Factory.CreateClient().WithUser();
        var resp = await client.PostAsync($"/v1/purchases/{Guid.NewGuid()}/refund", null);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StripeConfig_Anonymous_ReturnsOkOr503()
    {
        var client = db.Factory.CreateClient();
        var resp = await client.GetAsync("/v1/purchases/stripe-config");

        resp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task CancelBeacon_NoAuth_Returns401()
    {
        var client = db.Factory.CreateClient().WithoutAuth();
        var resp = await client.PostAsJsonAsync("/v1/purchases/cancel-beacon", new { purchaseId = Guid.NewGuid() });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
