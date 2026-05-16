using System.Net;
using System.Text.Json;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs;
using Contracts.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Middleware;

public sealed class ErrorHandlingMiddlewareUnitTests
{
    private static DefaultHttpContext BuildContext(IWebHostEnvironment env)
    {
        var services = new ServiceCollection();
        services.AddSingleton(env);
        services.AddSingleton<IAuditLogService, StubAuditLogService>();
        var provider = services.BuildServiceProvider();

        var ctx = new DefaultHttpContext
        {
            RequestServices = provider,
            Response = { Body = new MemoryStream() }
        };
        ctx.Request.Method = "GET";
        ctx.Request.Path = "/test";
        return ctx;
    }

#if !DEBUG
    [Fact]
    public async Task Production_DoesNotLeakExceptionMessage()
    {
        var env = new StubEnvironment("Production");
        var ctx = BuildContext(env);
        RequestDelegate next = _ => throw new InvalidOperationException("Secret internal error with SQL hints");

        var mw = new ErrorHandlingMiddleware(next);
        await mw.InvokeAsync(ctx, new StubAuditLogService());

        ctx.Response.StatusCode.Should().Be(500);
        ctx.Response.Body.Position = 0;
        var body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        body.Should().NotContain("Secret internal error");
        body.Should().NotContain("SQL");
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("message").GetString().Should().Be("An internal error occurred");
        doc.RootElement.TryGetProperty("correlationId", out var cid).Should().BeTrue();
        cid.GetString().Should().NotBeNullOrEmpty();
    }
#endif

#if DEBUG
    [Fact]
    public async Task Debug_IncludesExceptionDetail()
    {
        var env = new StubEnvironment("Development");
        var ctx = BuildContext(env);
        RequestDelegate next = _ => throw new InvalidOperationException("detail-for-dev");

        var mw = new ErrorHandlingMiddleware(next);
        await mw.InvokeAsync(ctx, new StubAuditLogService());

        ctx.Response.StatusCode.Should().Be(500);
        ctx.Response.Body.Position = 0;
        var body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        body.Should().Contain("detail-for-dev");
    }
#else
    [Fact]
    public async Task Release_OmitsExceptionDetailEvenInDevelopmentEnv()
    {
        var env = new StubEnvironment("Development");
        var ctx = BuildContext(env);
        RequestDelegate next = _ => throw new InvalidOperationException("Secret internal error with SQL hints");

        var mw = new ErrorHandlingMiddleware(next);
        await mw.InvokeAsync(ctx, new StubAuditLogService());

        ctx.Response.StatusCode.Should().Be(500);
        ctx.Response.Body.Position = 0;
        var body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        body.Should().NotContain("Secret internal error");
        body.Should().NotContain("SQL");
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("message").GetString().Should().Be("An internal error occurred");
    }
#endif

    private sealed class StubEnvironment(string name) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string WebRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class StubAuditLogService : IAuditLogService
    {
        public Task<Guid> LogAsync(
            string eventType,
            AuditActorType actorType,
            Guid? actorId = null,
            string? subjectType = null,
            Guid? subjectId = null,
            string? action = null,
            string? metadataJson = null,
            string? ip = null,
            Guid? correlationId = null,
            CancellationToken ct = default) => Task.FromResult(Guid.NewGuid());
    }
}
