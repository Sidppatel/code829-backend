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

    // Load .env file — always attempt to load so env vars like ASPNETCORE_ENVIRONMENT are available
    // even when launched via Start-Process which doesn't inherit parent shell env vars
    {
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
    else
    {
        builder.Services.AddScoped<IEmailService, SmtpEmailService>();
        builder.Services.AddScoped<IPaymentService, StripePaymentService>();
        builder.Services.AddScoped<IFileStorageService, S3FileStorageService>();
    }

    // Background workers
    builder.Services.AddHostedService<LogCleanupWorker>();
    builder.Services.AddHostedService<HoldCleanupWorker>();
    builder.Services.AddHostedService<ScheduledPublishWorker>();

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

    // Seed data (development only)
    if (app.Environment.IsDevelopment())
    {
        await DataSeeder.SeedAsync(app.Services);
        await VenueEventSeeder.SeedAsync(app.Services);
        await BookingSeeder.SeedAsync(app.Services);
    }
    else
    {
        // Production: only seed essential settings (jwt_secret etc.) if missing
        await DataSeeder.SeedAsync(app.Services);
    }

    // Configure JWT signing key from DB settings
    await ConfigureJwtSigningKey(app);

    // Middleware pipeline
    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseMiddleware<ErrorHandlingMiddleware>();

    // HTTPS redirect in production
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    // CORS — load allowed origins from DB settings
    app.UseCors(policy =>
    {
        using var scope = app.Services.CreateScope();
        var settingsSvc = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var originsStr = settingsSvc.GetOrDefaultAsync("cors_origins", "http://localhost:5173").Result ?? "http://localhost:5173";
        var origins = originsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });

    // Static files for uploads
    var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
    Directory.CreateDirectory(uploadsPath);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
        RequestPath = "/uploads"
    });

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<RoleAuthorizationMiddleware>();

    app.MapControllers();

    // OpenAPI + Scalar: always available but auth-protected in production
    app.MapOpenApi();
    if (app.Environment.IsDevelopment())
    {
        app.MapScalarApiReference();
    }
    else
    {
        // In production, Scalar requires authentication
        app.MapScalarApiReference().RequireAuthorization();
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
    // Disable SSL for local Docker PostgreSQL (alpine image has no SSL configured)
    return $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SslMode=Disable;Minimum Pool Size=5;Maximum Pool Size=50;Connection Idle Lifetime=60";
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
