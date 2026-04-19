using System.Net;
using Api.Middleware;
using Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Moq;
using StackExchange.Redis;

namespace Api.Tests;

public class RateLimitingMiddlewareTests
{
    private readonly Mock<IConnectionMultiplexer> _redis;
    private readonly Mock<IDatabase> _redisDb;
    private readonly Mock<IWebHostEnvironment> _env;
    private readonly Mock<ISettingsService> _settings;

    public RateLimitingMiddlewareTests()
    {
        _redis = new Mock<IConnectionMultiplexer>();
        _redisDb = new Mock<IDatabase>();
        _redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_redisDb.Object);
        _env = new Mock<IWebHostEnvironment>();
        _env.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        _settings = new Mock<ISettingsService>();
        // Default: rate limiting enabled
        _settings.Setup(s => s.GetOrDefaultAsync("rate_limit_disabled", "false")).ReturnsAsync("false");
    }

    [Fact]
    public async Task Request_BelowLimit_Returns200()
    {
        _redisDb.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(1);
        _redisDb.Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new RateLimitingMiddleware(next, _redis.Object, _env.Object, _settings.Object);
        var context = CreateHttpContext("192.168.1.1", "/events");

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().NotBe(429);
    }

    [Fact]
    public async Task Request_AboveLimit_Returns429()
    {
        _redisDb.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(201); // above default limit of 200
        _redisDb.Setup(d => d.KeyTimeToLiveAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(TimeSpan.FromSeconds(60));

        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new RateLimitingMiddleware(next, _redis.Object, _env.Object, _settings.Object);
        var context = CreateHttpContext("192.168.1.1", "/events");

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(429);
    }

    [Fact]
    public async Task Request_DifferentIPs_IndependentCounters()
    {
        var counters = new Dictionary<string, long>();
        _redisDb.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, long value, CommandFlags flags) =>
            {
                var k = key.ToString();
                counters[k] = counters.GetValueOrDefault(k, 0) + 1;
                return Task.FromResult(counters[k]);
            });
        _redisDb.Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new RateLimitingMiddleware(next, _redis.Object, _env.Object, _settings.Object);

        // Two different IPs
        await middleware.InvokeAsync(CreateHttpContext("10.0.0.1", "/events"));
        await middleware.InvokeAsync(CreateHttpContext("10.0.0.2", "/events"));

        // Each IP should have its own counter key
        counters.Should().HaveCount(2);
        counters.Keys.Should().Contain(k => k.Contains("10.0.0.1"));
        counters.Keys.Should().Contain(k => k.Contains("10.0.0.2"));
    }

    private static HttpContext CreateHttpContext(string ip, string path)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }
}
