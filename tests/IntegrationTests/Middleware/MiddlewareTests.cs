using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Contracts.DTOs;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Middleware;

[Collection("Database")]
public sealed class MiddlewareTests(DatabaseFixture db)
{
    [Fact]
    public async Task CorrelationId_AddedToResponseHeader()
    {
        var client = db.Factory.CreateClient();
        var resp = await client.GetAsync("/health/live");
        resp.Headers.Should().ContainKey("X-Correlation-Id");
        resp.Headers.GetValues("X-Correlation-Id").Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SecurityHeaders_SetOnAllResponses()
    {
        var client = db.Factory.CreateClient();
        var resp = await client.GetAsync("/health/live");

        resp.Headers.TryGetValues("X-Content-Type-Options", out var xcto);
        xcto.Should().Contain("nosniff");

        resp.Headers.TryGetValues("X-Frame-Options", out var xfo);
        xfo.Should().Contain("DENY");

        resp.Headers.TryGetValues("Referrer-Policy", out var rp);
        rp.Should().Contain("strict-origin-when-cross-origin");

        resp.Headers.Should().NotContainKey("X-Powered-By");
    }

    [Fact]
    public async Task SecurityHeaders_HstsAndCsp_NotSetInDevelopment()
    {

        var client = db.Factory.CreateClient();
        var resp = await client.GetAsync("/health/live");
        resp.Headers.Should().NotContainKey("Strict-Transport-Security");
        resp.Headers.Should().NotContainKey("Content-Security-Policy");
    }

    [Fact]
    public async Task ErrorHandling_Returns404_ShapeForUnknownRoute()
    {
        var client = db.Factory.CreateClient();
        var resp = await client.GetAsync("/this-route-does-not-exist-12345");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ApiError_ShapeIncludesTraceId_On403()
    {

        var client = db.Factory.CreateClient().WithUser();
        var resp = await client.GetAsync("/v1/admin/auth/me");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var err = await resp.Content.ReadFromJsonAsync<ApiError>(JsonSerializerOptions.Web);
        err.Should().NotBeNull();
        err!.Message.Should().NotBeNullOrEmpty();
        err.CorrelationId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RateLimiting_EnforcedOnAuthRoutes()
    {
        var client = db.Factory.CreateClient();

        var email = $"ratelimit-{Guid.NewGuid():N}@example.com";
        HttpStatusCode? lastCode = null;
        for (var i = 0; i < 6; i++)
        {
            var resp = await client.PostAsJsonAsync("/v1/auth/magic-link", new { email });
            lastCode = resp.StatusCode;
        }
        lastCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
