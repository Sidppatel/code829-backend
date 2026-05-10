using Db;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace IntegrationTests.Fixtures;

/// <summary>
/// WebApplicationFactory that replaces the real Postgres + Redis with Testcontainer instances.
/// Env vars are pre-set by DatabaseFixture before this factory's host is built.
/// ConfigureTestServices provides belt-and-suspenders service override.
/// </summary>
public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    private readonly string _postgresConnectionString;
    private readonly string _redisUrl;

    public TestApiFactory(string postgresConnectionString, string redisUrl)
    {
        _postgresConnectionString = postgresConnectionString;
        _redisUrl = redisUrl;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {

            var dbDescriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<EventPlatformDbContext>)
                         || d.ServiceType == typeof(EventPlatformDbContext))
                .ToList();
            foreach (var d in dbDescriptors) services.Remove(d);

            services.AddDbContext<EventPlatformDbContext>(options =>
                options.UseNpgsql(_postgresConnectionString));

            var redisDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IConnectionMultiplexer));
            if (redisDescriptor is not null) services.Remove(redisDescriptor);

            var redisConfig = ParseRedisUrl(_redisUrl);
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConfig));
        });
    }

    private static string ParseRedisUrl(string url)
    {
        var uri = new Uri(url);
        var config = $"{uri.Host}:{uri.Port}";
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var parts = uri.UserInfo.Split(':');
            if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
                config += $",password={parts[1]}";
        }
        return config;
    }
}
