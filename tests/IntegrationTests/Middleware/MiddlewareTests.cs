using System.Net;
using System.Net.Http.Json;
using Contracts.DTOs;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Middleware;

/// <summary>
/// Integration tests for the full middleware pipeline via WebApplicationFactory.
/// Covers: CorrelationId, SecurityHeaders, ErrorHandling, RateLimiting.
/// </summary>
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
        // DatabaseFixture pins ASPNETCORE_ENVIRONMENT=Development
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
    public async Task ApiError_ShapeIncludesTraceId_On401()
    {
        var client = db.Factory.CreateClient().WithoutAuth();
        var resp = await client.GetAsync("/admin/auth/me");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var err = await resp.Content.ReadFromJsonAsync<ApiError>();
        err.Should().NotBeNull();
        err!.Message.Should().NotBeNullOrEmpty();
        err.CorrelationId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RateLimiting_EnforcedOnAuthRoutes()
    {
        var client = db.Factory.CreateClient();
        // Magic link endpoint is limited to 2 per 2-minute window per email
        var email = $"ratelimit-{Guid.NewGuid():N}@example.com";
        HttpStatusCode? lastCode = null;
        for (var i = 0; i < 6; i++)
        {
            var resp = await client.PostAsJsonAsync("/auth/magic-link", new { email });
            lastCode = resp.StatusCode;
        }
        lastCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
