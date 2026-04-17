using Api.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace Api.Tests;

public class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task Production_CspIncludesStripeDomains()
    {
        var context = CreateHttpContext(Environments.Production);
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask, Options.Create(new SecurityHeadersOptions()));

        await middleware.InvokeAsync(context);

        var csp = context.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("https://js.stripe.com");
        csp.Should().Contain("https://api.stripe.com");
        csp.Should().Contain("https://hooks.stripe.com");
        csp.Should().Contain("frame-src");
        csp.Should().Contain("script-src");
        csp.Should().Contain("connect-src");
    }

    [Fact]
    public async Task Production_SetsHsts()
    {
        var context = CreateHttpContext(Environments.Production);
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask, Options.Create(new SecurityHeadersOptions()));

        await middleware.InvokeAsync(context);

        context.Response.Headers["Strict-Transport-Security"].ToString().Should().Contain("max-age=31536000");
    }

    [Fact]
    public async Task Development_SkipsCspAndHsts()
    {
        var context = CreateHttpContext(Environments.Development);
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask, Options.Create(new SecurityHeadersOptions()));

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContainsKey("Content-Security-Policy").Should().BeFalse();
        context.Response.Headers.ContainsKey("Strict-Transport-Security").Should().BeFalse();
    }

    [Fact]
    public async Task Production_WithEnableFlagOff_SkipsCspAndHsts()
    {
        var context = CreateHttpContext(Environments.Production);
        var opts = Options.Create(new SecurityHeadersOptions { EnableHstsAndCsp = false });
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask, opts);

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContainsKey("Content-Security-Policy").Should().BeFalse();
        context.Response.Headers.ContainsKey("Strict-Transport-Security").Should().BeFalse();
    }

    [Fact]
    public async Task AlwaysSetsBaseHeaders()
    {
        var context = CreateHttpContext(Environments.Development);
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask, Options.Create(new SecurityHeadersOptions()));

        await middleware.InvokeAsync(context);

        context.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
        context.Response.Headers["X-Frame-Options"].ToString().Should().Be("DENY");
        context.Response.Headers["Referrer-Policy"].ToString().Should().Be("strict-origin-when-cross-origin");
    }

    private static HttpContext CreateHttpContext(string environmentName)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(environmentName);

        var services = new ServiceCollection();
        services.AddSingleton(env.Object);
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = provider };
        context.Response.Body = new MemoryStream();
        return context;
    }
}
