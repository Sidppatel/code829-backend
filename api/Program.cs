using System.Text;
using Api.Middleware;
using Api.Seeding;
using Api.Services;
using Api.Workers;
using Db;
using Db.Interceptors;
using Db.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using StackExchange.Redis;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Load .env file in development
    if (builder.Environment.IsDevelopment())
    {
        // Try multiple possible .env locations: parent of content root (backend/) and current dir
        var envCandidates = new[]
        {
            Path.Combine(builder.Environment.ContentRootPath, "..", ".env"),
            Path.Combine(Directory.GetCurrentDirectory(), ".env"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", ".env"),
        };
        var envPath = envCandidates.FirstOrDefault(File.Exists);
        if (envPath is not null)
        {
            foreach (var line in File.ReadAllLines(envPath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                    continue;
                var eqIndex = trimmed.IndexOf('=');
                if (eqIndex <= 0) continue;
                var key = trimmed[..eqIndex];
                var value = trimmed[(eqIndex + 1)..];
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    // Serilog
    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console());

    // Kestrel on configurable port
    var port = Environment.GetEnvironmentVariable("PORT") ?? "8000";
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

    // Database
    var dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
        ?? throw new InvalidOperationException("DATABASE_URL is required");
    var connStr = ConvertPostgresUrl(dbUrl);

    builder.Services.AddSingleton<ChangeTrackingInterceptor>();
    builder.Services.AddDbContext<EventPlatformDbContext>((sp, options) =>
    {
        options.UseNpgsql(connStr);
    });

    // Redis
    var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL") ?? "redis://localhost:6379";
    var redisConfig = ConvertRedisUrl(redisUrl);
    builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConfig));

    // Encryption
    builder.Services.AddSingleton<IEncryptionService, EncryptionService>();

    // Repositories
    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IAppSettingRepository, AppSettingRepository>();
    builder.Services.AddScoped<ILogRepository, LogRepository>();

    // Services
    builder.Services.AddScoped<ISettingsService, SettingsService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<ISeatService, SeatService>();
    builder.Services.AddScoped<IPricingEngine, PricingEngine>();
    builder.Services.AddScoped<IBookingService, BookingService>();

    // Conditional service registration: mock in dev, real in prod
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddScoped<IEmailService, MockEmailService>();
        builder.Services.AddScoped<IPaymentService, MockPaymentService>();
        builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
    }
    // Production implementations would be registered here in else block

    // Background workers
    builder.Services.AddHostedService<LogCleanupWorker>();
    builder.Services.AddHostedService<HoldCleanupWorker>();

    // JWT Authentication — uses a temporary key at startup, replaced by DB-stored secret after seeding
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            // Defer key resolution to allow reading from DB settings at runtime
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = _ => Task.CompletedTask
            };
        });
    builder.Services.AddAuthorization();

    // Controllers + OpenAPI
    builder.Services.AddControllers();
    builder.Services.AddOpenApi();

    // CORS — configured from settings, defaults to localhost:5173
    builder.Services.AddCors();

    var app = builder.Build();

    // Apply migrations
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<EventPlatformDbContext>();
        await db.Database.MigrateAsync();
    }

    // Seed data
    await DataSeeder.SeedAsync(app.Services);
    await VenueEventSeeder.SeedAsync(app.Services);
    await BookingSeeder.SeedAsync(app.Services);

    // Configure JWT signing key from DB settings
    await ConfigureJwtSigningKey(app);

    // Middleware pipeline
    app.UseMiddleware<ErrorHandlingMiddleware>();

    // CORS
    app.UseCors(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });

    // Static files for uploads
    if (app.Environment.IsDevelopment())
    {
        var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
        Directory.CreateDirectory(uploadsPath);
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
            RequestPath = "/uploads"
        });
    }

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<RoleAuthorizationMiddleware>();

    app.MapControllers();

    // OpenAPI + Scalar (dev only)
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    Log.Information("Event Platform API starting on port {Port}", port);
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>
/// Converts a postgres:// URL to an ADO.NET connection string for Npgsql.
/// </summary>
static string ConvertPostgresUrl(string url)
{
    var uri = new Uri(url);
    var userInfo = uri.UserInfo.Split(':');
    return $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};Trust Server Certificate=true";
}

/// <summary>
/// Converts a redis:// URL to a StackExchange.Redis configuration string.
/// </summary>
static string ConvertRedisUrl(string url)
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

/// <summary>
/// Reads the JWT secret from DB settings and configures the JWT bearer middleware.
/// </summary>
static async Task ConfigureJwtSigningKey(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
    var jwtSecret = await settings.GetAsync("jwt_secret");

    var jwtOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<JwtBearerOptions>>();
    var bearerOptions = jwtOptions.Get(JwtBearerDefaults.AuthenticationScheme);
    bearerOptions.TokenValidationParameters.IssuerSigningKey =
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
}
