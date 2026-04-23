using System.Net;
using System.Text.Json;
using Api.Middleware;
using Contracts.DTOs;
using Db.Entities;
using Db.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Middleware;

/// <summary>
/// Unit tests for ErrorHandlingMiddleware — assert no stack traces leak outside Development
/// and ProblemDetails/ApiError shape is correct.
/// </summary>
public sealed class ErrorHandlingMiddlewareUnitTests
{
    private static DefaultHttpContext BuildContext(IWebHostEnvironment env)
    {
        var services = new ServiceCollection();
        services.AddSingleton(env);
        services.AddSingleton<ILogRepository, StubLogRepository>();
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

    [Fact]
    public async Task Production_DoesNotLeakExceptionMessage()
    {
        var env = new StubEnvironment("Production");
        var ctx = BuildContext(env);
        RequestDelegate next = _ => throw new InvalidOperationException("Secret internal error with SQL hints");

        var mw = new ErrorHandlingMiddleware(next);
        await mw.InvokeAsync(ctx, new StubLogRepository());

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

    [Fact]
    public async Task Development_IncludesExceptionDetail()
    {
        var env = new StubEnvironment("Development");
        var ctx = BuildContext(env);
        RequestDelegate next = _ => throw new InvalidOperationException("detail-for-dev");

        var mw = new ErrorHandlingMiddleware(next);
        await mw.InvokeAsync(ctx, new StubLogRepository());

        ctx.Response.StatusCode.Should().Be(500);
        ctx.Response.Body.Position = 0;
        var body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        body.Should().Contain("detail-for-dev");
    }

    private sealed class StubEnvironment(string name) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string WebRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class StubLogRepository : ILogRepository
    {
        public Task AddDeveloperLogAsync(DeveloperLog log) => Task.CompletedTask;
        public Task AddAdminLogAsync(BusinessLog log) => Task.CompletedTask;
        public Task AddSystemLogAsync(SystemLog log) => Task.CompletedTask;
        public Task AddEmailLogAsync(EmailLog log) => Task.CompletedTask;
        public Task<int> CleanupDeveloperLogsAsync(int retentionDays) => Task.FromResult(0);
        public Task<int> CleanupAdminLogsAsync(int retentionDays) => Task.FromResult(0);
        public Task<int> CleanupSystemLogsAsync(int retentionDays) => Task.FromResult(0);
    }
}
