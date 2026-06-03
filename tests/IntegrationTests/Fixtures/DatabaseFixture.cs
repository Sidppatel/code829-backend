using System.Diagnostics;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace IntegrationTests.Fixtures;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private const string DbRepoUrl = "git@github.com:Sidppatel/code829-db.git";
    private const string DbRef = "main";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("ep_test")
        .WithUsername("ep_test")
        .WithPassword("ep_test")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine")
        .Build();

    public string PostgresConnectionString { get; private set; } = "";
    public string RedisConfig { get; private set; } = "";

    public TestApiFactory Factory { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        PostgresConnectionString = _postgres.GetConnectionString();
        var redisPort = _redis.GetMappedPublicPort(6379);
        RedisConfig = $"localhost:{redisPort}";




        var pg = new NpgsqlConnectionStringBuilder(PostgresConnectionString);
        Environment.SetEnvironmentVariable("DB_HOST", pg.Host);
        Environment.SetEnvironmentVariable("DB_PORT", (pg.Port == 0 ? 5432 : pg.Port).ToString());
        Environment.SetEnvironmentVariable("DB_USER", pg.Username);
        Environment.SetEnvironmentVariable("DB_NAME", pg.Database);
        Environment.SetEnvironmentVariable("DB_PASSWORD", pg.Password);
        Environment.SetEnvironmentVariable("DATABASE_SSL_MODE", "Disable");

        Environment.SetEnvironmentVariable("REDIS_HOST", "localhost");
        Environment.SetEnvironmentVariable("REDIS_PORT", redisPort.ToString());
        Environment.SetEnvironmentVariable("REDIS_USER", null);
        Environment.SetEnvironmentVariable("REDIS_PASSWORD", null);
        Environment.SetEnvironmentVariable("REDIS_TLS", "false");

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("JWT_SECRET", "integration-test-jwt-secret-must-be-32-chars!!");

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY")))
            Environment.SetEnvironmentVariable("STRIPE_SECRET_KEY", "sk_test_integration_placeholder");

        Environment.SetEnvironmentVariable("STRIPE_WEBHOOK_SECRET", "whsec_integration_test_secret");

        // Create the authenticated role and auth schema/functions required by Supabase RLS policies
        await ExecuteSqlAsync(@"
            CREATE SCHEMA IF NOT EXISTS auth;
            CREATE OR REPLACE FUNCTION auth.uid() RETURNS uuid LANGUAGE sql STABLE AS 'SELECT null::uuid';
            
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'authenticated') THEN
                    CREATE ROLE authenticated;
                END IF;
            END
            $$;
        ");

        await RunMigrationsAsync();

        Factory = new TestApiFactory(PostgresConnectionString, RedisConfig);
    }

    public async ValueTask DisposeAsync()
    {
        Factory?.Dispose();
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }

    private async Task RunMigrationsAsync()
    {
        var runnerDll = await ResolveMigrationRunnerAsync();
        var migrationUrl = NpgsqlKvToUrl(PostgresConnectionString);

        var psi = new ProcessStartInfo("dotnet", $"\"{runnerDll}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["MIGRATION_DATABASE_URL"] = migrationUrl;

        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start dotnet for MigrationRunner");

        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();

        if (p.ExitCode != 0)
            throw new InvalidOperationException(
                $"MigrationRunner exited {p.ExitCode}\nstdout:\n{await stdout}\nstderr:\n{await stderr}");

    }

    private static async Task<string> ResolveMigrationRunnerAsync()
    {
        var pre = Environment.GetEnvironmentVariable("MIGRATION_RUNNER_DLL");
        if (!string.IsNullOrEmpty(pre) && File.Exists(pre))
            return pre;

        var cacheDir = Path.Combine(Path.GetTempPath(), "ep-code829-db-migrate");
        var dll = Path.Combine(cacheDir, "MigrationRunner.dll");
        if (File.Exists(dll))
        {
            Environment.SetEnvironmentVariable("MIGRATION_RUNNER_DLL", dll);
            return dll;
        }

        var srcDir = Path.Combine(Path.GetTempPath(), "ep-code829-db-src");
        if (Directory.Exists(srcDir)) Directory.Delete(srcDir, recursive: true);
        await RunAsync("git", $"clone --depth 1 --branch {DbRef} {DbRepoUrl} \"{srcDir}\"");
        await RunAsync("dotnet", $"publish \"{Path.Combine(srcDir, "src", "MigrationRunner")}\" -c Release -o \"{cacheDir}\" -p:UseAppHost=false");

        if (!File.Exists(dll))
            throw new InvalidOperationException($"MigrationRunner publish did not produce {dll}");

        Environment.SetEnvironmentVariable("MIGRATION_RUNNER_DLL", dll);
        return dll;
    }

    private static async Task RunAsync(string file, string args)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {file}");
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        if (p.ExitCode != 0)
            throw new InvalidOperationException(
                $"{file} {args} exited {p.ExitCode}\nstdout:\n{await stdout}\nstderr:\n{await stderr}");
    }

    private static string NpgsqlKvToUrl(string kv)
    {
        var b = new NpgsqlConnectionStringBuilder(kv);
        var user = Uri.EscapeDataString(b.Username ?? "");
        var pass = Uri.EscapeDataString(b.Password ?? "");
        var host = b.Host ?? "localhost";
        var port = b.Port == 0 ? 5432 : b.Port;
        var db = b.Database ?? "postgres";
        return $"postgresql://{user}:{pass}@{host}:{port}/{db}";
    }

    public async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var conn = new NpgsqlConnection(PostgresConnectionString);
        await conn.OpenAsync();
        return conn;
    }

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
