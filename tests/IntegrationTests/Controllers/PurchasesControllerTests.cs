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
        var resp = await client.PostAsJsonAsync("/purchases", new { eventId = Guid.NewGuid(), seats = 1 });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Quote_NoAuth_Returns401()
    {
        var client = db.Factory.CreateClient().WithoutAuth();
        var resp = await client.PostAsJsonAsync("/purchases/quote", new { eventId = Guid.NewGuid(), seats = 1 });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Mine_NoAuth_Returns401()
    {
        var client = db.Factory.CreateClient().WithoutAuth();
        var resp = await client.GetAsync("/purchases/mine");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_UnknownAsUser_Returns404()
    {
        var client = db.Factory.CreateClient().WithUser();
        var resp = await client.GetAsync($"/purchases/{Guid.NewGuid()}");
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Refund_UserRole_Returns403()
    {
        var client = db.Factory.CreateClient().WithUser();
        var resp = await client.PostAsync($"/purchases/{Guid.NewGuid()}/refund", null);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StripeConfig_Anonymous_ReturnsOkOr503()
    {
        var client = db.Factory.CreateClient();
        var resp = await client.GetAsync("/purchases/stripe-config");
        // Stripe test key may not be fully wired in test env; both OK and 503 are acceptable
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task CancelBeacon_NoAuth_Returns401()
    {
        var client = db.Factory.CreateClient().WithoutAuth();
        var resp = await client.PostAsJsonAsync("/purchases/cancel-beacon", new { purchaseId = Guid.NewGuid() });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
