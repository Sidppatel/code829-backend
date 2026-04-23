using Db;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace IntegrationTests.Fixtures;

/// <summary>
/// Shared fixture that boots postgres:16-alpine + redis:7-alpine via Testcontainers,
/// runs EF Core migrations, and seeds all stored procedures from the db assembly.
/// Shared across all test classes in the "Database" collection to avoid cold-start overhead.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("ep_test")
        .WithUsername("ep_test")
        .WithPassword("ep_test")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine")
        .Build();

    public string PostgresConnectionString { get; private set; } = "";
    public string RedisConnectionString { get; private set; } = "";

    public TestApiFactory Factory { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        PostgresConnectionString = _postgres.GetConnectionString();
        RedisConnectionString = $"redis://localhost:{_redis.GetMappedPublicPort(6379)}";

        // Set env vars before the WebApplicationFactory builds its host
        Environment.SetEnvironmentVariable("DATABASE_URL", PostgresConnectionString);
        Environment.SetEnvironmentVariable("REDIS_URL", RedisConnectionString);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("JWT_SECRET", "integration-test-jwt-secret-must-be-32-chars!!");
        // Use Stripe test placeholder — integration tests don't call Stripe directly
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY")))
            Environment.SetEnvironmentVariable("STRIPE_SECRET_KEY", "sk_test_integration_placeholder");

        await RunMigrationsAsync();
        await SeedStoredProceduresAsync();

        Factory = new TestApiFactory(PostgresConnectionString, RedisConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        Factory?.Dispose();
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }

    private async Task RunMigrationsAsync()
    {
        var options = new DbContextOptionsBuilder<EventPlatformDbContext>()
            .UseNpgsql(PostgresConnectionString)
            .Options;

        await using var ctx = new EventPlatformDbContext(options);
        await ctx.Database.MigrateAsync();
    }

    private async Task SeedStoredProceduresAsync()
    {
        // EventPlatformDbContext lives in the db project which has the embedded SQL resources
        var asm = typeof(EventPlatformDbContext).Assembly;
        var prefix = $"{asm.GetName().Name}.Sql.Procedures.";

        var sqlFiles = asm.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                     && n.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);

        await using var conn = new NpgsqlConnection(PostgresConnectionString);
        await conn.OpenAsync();

        foreach (var name in sqlFiles)
        {
            using var stream = asm.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            var sql = await reader.ReadToEndAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>Opens a raw Npgsql connection to the test database.</summary>
    public async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var conn = new NpgsqlConnection(PostgresConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    /// <summary>Executes raw SQL against the test database and returns rows as dynamic.</summary>
    public async Task ExecuteSqlAsync(string sql, params (string name, object? value)[] parameters)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in parameters)
            cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition("Database")]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture> { }
