using Db;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace IntegrationTests.Fixtures;

public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    private readonly string _postgresConnectionString;
    private readonly string _redisConfig;

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
