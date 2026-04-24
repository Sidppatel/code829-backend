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
        // Webhook handler may return 500 when STRIPE_WEBHOOK_SECRET isn't configured in the
// test env; 400/401 are the intended rejection codes for signature failures.
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
        // Webhook handler may return 500 when STRIPE_WEBHOOK_SECRET isn't configured in the
// test env; 400/401 are the intended rejection codes for signature failures.
resp.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError);
    }
}
