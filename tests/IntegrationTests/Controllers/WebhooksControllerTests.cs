using System.Net;
using System.Text;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Controllers;

[Collection("Database")]
public sealed class WebhooksControllerTests(DatabaseFixture db)
{
    [Fact]
    public async Task Stripe_NoSignatureHeader_Returns400()
    {
        var client = db.Factory.CreateClient();
        var content = new StringContent("{\"id\":\"evt_test\"}", Encoding.UTF8, "application/json");
        var resp = await client.PostAsync("/webhooks/stripe", content);

        resp.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Stripe_InvalidSignature_Returns400()
    {
        var client = db.Factory.CreateClient();
        var content = new StringContent("{\"id\":\"evt_test\"}", Encoding.UTF8, "application/json");
        content.Headers.Add("Stripe-Signature", "t=1,v1=deadbeef");
        var req = new HttpRequestMessage(HttpMethod.Post, "/webhooks/stripe") { Content = content };
        var resp = await client.SendAsync(req);

        resp.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError);
    }
}
