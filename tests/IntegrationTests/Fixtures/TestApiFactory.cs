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
/// Component env vars are pre-set by DatabaseFixture before this factory's host is built.
/// ConfigureTestServices provides belt-and-suspenders service override using the
/// Testcontainer-supplied connection strings directly — no URL form anywhere.
/// </summary>
public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    private readonly string _postgresConnectionString;
    private readonly string _redisConfig;

    /// <param name="postgresConnectionString">Npgsql kv-form string from Testcontainers.</param>
    /// <param name="redisConfig">StackExchange.Redis configuration string (host:port[,password=...][,ssl=true]).</param>
    public TestApiFactory(string postgresConnectionString, string redisConfig)
    {
        _postgresConnectionString = postgresConnectionString;
        _redisConfig = redisConfig;
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

            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(_redisConfig));
        });
    }
}
