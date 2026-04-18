using System.Text;
using Api.Middleware;
using Api.Seeding;
using Api.Services;
using Api.Validators;
using Api.Workers;
using Contracts.DTOs.Auth;
using Contracts.DTOs.Bookings;
using Contracts.DTOs.Events;
using System.IO.Compression;
using Db;
using Db.Repositories;
using Microsoft.AspNetCore.ResponseCompression;
using FluentValidation;
using Api.Filters;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using StackExchange.Redis;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
var bootstrapEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
if (bootstrapEnv == "Development")
{
    var envCandidates = new[]
    {
        Path.Combine(Directory.GetCurrentDirectory(), "..", ".env"),
        Path.Combine(Directory.GetCurrentDirectory(), ".env"),
    };
    var envPath = envCandidates.FirstOrDefault(File.Exists);
    if (envPath is not null)
    {
        Console.WriteLine($"[Bootstrap] Loading environment from {envPath}");
        foreach (var line in File.ReadAllLines(envPath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;
            var eqIndex = trimmed.IndexOf('=');
            if (eqIndex <= 0) continue;
            var key = trimmed[..eqIndex];
            var value = trimmed[(eqIndex + 1)..];
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}

var builder = WebApplication.CreateBuilder(args);


    // Serilog — structured logging to console + files with timestamps
    builder.Host.UseSerilog((ctx, lc) =>
    {
        lc.ReadFrom.Configuration(ctx.Configuration)
          .Enrich.WithMachineName()
          .Enrich.FromLogContext()
          .MinimumLevel.Information()
          .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
          .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning);

        // Console: include CorrelationId so a payment issue in logs can be traced back to the
        // request. UserId comes from CorrelationIdMiddleware too.
        lc.WriteTo.Console(
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}");

        // Main API log file
        lc.WriteTo.File("logs/api-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{CorrelationId}] [{UserId}] {SourceContext}{NewLine}  {Message:lj}{NewLine}{Exception}");

        // Error-only log file for quick triage
        lc.WriteTo.Logger(lc2 => lc2
            .Filter.ByIncludingOnly(le => le.Level >= Serilog.Events.LogEventLevel.Error)
            .WriteTo.File("logs/errors-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{CorrelationId}] [{UserId}] {SourceContext}{NewLine}  {Message:lj}{NewLine}{Exception}"));

        // Separate file for seeding operations
        lc.WriteTo.Logger(lc2 => lc2
            .Filter.ByIncludingOnly(le => le.MessageTemplate.Text.Contains("[Seed]"))
            .WriteTo.File("logs/seeding-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} {Message:lj}{NewLine}{Exception}"));
    });

    // Kestrel on configurable port with request size limits
    var port = Environment.GetEnvironmentVariable("PORT") ?? "8000";
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = 15 * 1024 * 1024; // 15 MB global limit
    });

    // Database
    var dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
        ?? throw new InvalidOperationException("DATABASE_URL is required");
    var connStr = ConvertPostgresUrl(dbUrl);

    builder.Services.AddDbContext<EventPlatformDbContext>((sp, options) =>
    {
        options.UseNpgsql(connStr);
        options.ConfigureWarnings(w =>
            w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.FirstWithoutOrderByAndFilterWarning));
    });

    // Redis
    var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL") ?? "redis://localhost:6379";
    var redisConfig = ConvertRedisUrl(redisUrl);
    builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConfig));

    // Encryption (HashEmail only — secrets now come from env vars)
    builder.Services.AddSingleton<IEncryptionService, EncryptionService>();

    // Secrets from environment variables
    builder.Services.AddSingleton<ISecretsProvider, SecretsProvider>();

    // Security headers / CSP — defaults are baked into SecurityHeadersOptions so that
    // missing/empty appsettings cannot silently strip Stripe domains from the policy.
    // Override in appsettings.*.json under "Security:Csp".
    builder.Services.Configure<SecurityHeadersOptions>(
        builder.Configuration.GetSection("Security:Csp"));

    // Forwarded headers — rewrite Connection.RemoteIpAddress from X-Forwarded-For so
    // rate limiting and audit logs see the real client IP when deployed behind a LB/CDN.
    // Trusted proxy CIDRs come from the TRUSTED_PROXIES env var.
    builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
        ForwardedHeadersConfig.Configure(
            options,
            builder.Environment.IsDevelopment(),
            Environment.GetEnvironmentVariable("TRUSTED_PROXIES")));

    // Fail-fast: Stripe is mandatory in every environment. No mock payment path exists.
    // Read directly from Environment to honor .env files loaded above after CreateBuilder.
    var stripeSecret = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY");
    if (string.IsNullOrEmpty(stripeSecret))
        throw new InvalidOperationException(
            "STRIPE_SECRET_KEY is required. Set test keys in .env for development or live keys in production — see .env.example.");
    if (builder.Environment.IsProduction() && !stripeSecret.StartsWith("sk_live_"))
        throw new InvalidOperationException(
            "Production environment requires a live Stripe secret key (sk_live_*). Refusing to start with test keys.");

    // Repositories
    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IAppSettingRepository, AppSettingRepository>();
    builder.Services.AddScoped<ILogRepository, LogRepository>();

    // SP Repositories
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IAuthProcedures, Db.Repositories.StoredProcedures.AuthProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IUserProcedures, Db.Repositories.StoredProcedures.UserProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IEventProcedures, Db.Repositories.StoredProcedures.EventProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IVenueProcedures, Db.Repositories.StoredProcedures.VenueProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.ITableProcedures, Db.Repositories.StoredProcedures.TableProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IBookingProcedures, Db.Repositories.StoredProcedures.BookingProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.ITicketProcedures, Db.Repositories.StoredProcedures.TicketProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IStripeTransactionProcedures, Db.Repositories.StoredProcedures.StripeTransactionProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IImageProcedures, Db.Repositories.StoredProcedures.ImageProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.ISettingsProcedures, Db.Repositories.StoredProcedures.SettingsProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.ILogProcedures, Db.Repositories.StoredProcedures.LogProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IFeedbackProcedures, Db.Repositories.StoredProcedures.FeedbackProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IEventTicketTypeProcedures, Db.Repositories.StoredProcedures.EventTicketTypeProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IAdminUserProcedures, Db.Repositories.StoredProcedures.AdminUserProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IInvitationProcedures, Db.Repositories.StoredProcedures.InvitationProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.ILayoutProcedures, Db.Repositories.StoredProcedures.LayoutProcedures>();

    // Services
    builder.Services.AddScoped<ISettingsService, SettingsService>();
    builder.Services.AddScoped<IJwtService, JwtService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IAdminAuthService, AdminAuthService>();
    builder.Services.AddScoped<IInvitationService, InvitationService>();
    builder.Services.AddScoped<ITableBookingService, TableBookingService>();
    builder.Services.AddScoped<IBookingService, BookingService>();
    builder.Services.AddScoped<IAdminLogService, AdminLogService>();
    builder.Services.AddScoped<IImageRepository, ImageRepository>();
    builder.Services.AddScoped<IImageProcessingService, ImageProcessingService>();
    builder.Services.AddScoped<IImageService, ImageService>();
    builder.Services.AddScoped<ICacheService, RedisCacheService>();

    // Conditional service registration: mock in dev, real in prod
    // Payment service uses real Stripe when a valid key is configured, even in dev
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
    }
    else
    {
        // Fail-fast: an empty access/secret/bucket would construct an AmazonS3Client that
        // only fails at upload time, crashing user-facing requests instead of the process.
        var s3Access = Environment.GetEnvironmentVariable("S3_ACCESS_KEY");
        var s3Secret = Environment.GetEnvironmentVariable("S3_SECRET_KEY");
        var s3Bucket = Environment.GetEnvironmentVariable("S3_BUCKET");
        if (string.IsNullOrEmpty(s3Access) || string.IsNullOrEmpty(s3Secret) || string.IsNullOrEmpty(s3Bucket))
            throw new InvalidOperationException(
                "S3_ACCESS_KEY, S3_SECRET_KEY, and S3_BUCKET are required outside Development. See .env.example.");

        builder.Services.AddScoped<IFileStorageService, S3FileStorageService>();
    }

    builder.Services.AddScoped<IEmailService>(sp =>
    {
        var secretsProvider = sp.GetRequiredService<ISecretsProvider>();
        var logProc = sp.GetRequiredService<Db.Repositories.StoredProcedures.ILogProcedures>();

        if (!string.IsNullOrEmpty(secretsProvider.ResendApiKey))
            return new ResendEmailService(secretsProvider, sp.GetRequiredService<ISettingsService>(), logProc);

        return new MockEmailService(logProc);
    });

    builder.Services.AddScoped<IPaymentService, StripePaymentService>();
    builder.Services.AddScoped<ITaxService, StripeTaxService>();
    builder.Services.AddScoped<IPricingService, PricingService>();

    // Background workers
    builder.Services.AddHostedService<LogCleanupWorker>();
    builder.Services.AddHostedService<HoldCleanupWorker>();
    builder.Services.AddHostedService<ScheduledPublishWorker>();

    // JWT Authentication — signing key configured from JWT_SECRET env var after build
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidIssuer = "code829-api",
                ValidAudience = "code829-client",
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

    // Controllers + OpenAPI + Validation
    // CSRF note: Antiforgery tokens are designed for server-rendered forms, not SPA + JWT APIs.
    // This API is protected by JWT auth + SameSite cookies + CORS origin checks instead.
    builder.Services.AddControllers(options =>
        {
            // Replaces AddFluentValidationAutoValidation() from the deprecated
            // FluentValidation.AspNetCore package. See api/Filters/FluentValidationFilter.cs.
            options.Filters.Add<FluentValidationFilter>();
        })
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });
    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = Asp.Versioning.ApiVersionReader.Combine(
            new Asp.Versioning.HeaderApiVersionReader("X-Api-Version"),
            new Asp.Versioning.QueryStringApiVersionReader("api-version"));
    });
    builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .ToDictionary(
                    e => e.Key,
                    e => e.Value!.Errors.Select(er => er.ErrorMessage).ToArray());

            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new
            {
                statusCode = 400,
                message = "Validation failed",
                errors,
                correlationId = context.HttpContext.TraceIdentifier,
            });
        };
    });
    builder.Services.AddOpenApi();
    builder.Services.AddScoped<IValidator<MagicLinkRequest>, MagicLinkRequestValidator>();
    builder.Services.AddScoped<IValidator<CreateBookingRequest>, CreateBookingRequestValidator>();
    builder.Services.AddScoped<IValidator<CreateEventRequest>, CreateEventRequestValidator>();

    // CORS — configured from settings, defaults to localhost:5173
    builder.Services.AddCors();

    // Response compression (Brotli + gzip)
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
        options.Providers.Add<BrotliCompressionProvider>();
        options.Providers.Add<GzipCompressionProvider>();
    });
    builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
    builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

    var app = builder.Build();

    // Apply pending migrations on every startup — with retry for slow DB startup
    const int maxRetries = 5;
    for (var attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EventPlatformDbContext>();
            await db.Database.MigrateAsync();
            Log.Information("Database migrations applied");
            break;
        }
        catch (Npgsql.NpgsqlException ex) when (attempt < maxRetries)
        {
            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
            Log.Warning(ex, "Database not ready (attempt {Attempt}/{Max}), retrying in {Delay}s...",
                attempt, maxRetries, delay.TotalSeconds);
            await Task.Delay(delay);
        }
    }

    // Seed data — only in development to prevent test data in production
    if (app.Environment.IsDevelopment())
    {
        await DataSeeder.SeedAsync(app.Services);
        await VenueEventSeeder.SeedAsync(app.Services);
        await LayoutSeeder.SeedAsync(app.Services);
        await BookingSeeder.SeedAsync(app.Services);
    }

    // Configure JWT signing key from DB settings
    await ConfigureJwtSigningKey(app);

    // Pre-load CORS origins to avoid async deadlock (.Result anti-pattern)
    string[] corsOrigins;
    {
        using var scope = app.Services.CreateScope();
        var settingsSvc = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var defaultOrigins = app.Environment.IsDevelopment()
            ? "http://localhost:5173,http://localhost:5174,http://localhost:5175,http://localhost:5176"  // Dev: public, admin, staff, developer ports
            : "http://localhost:5173";
        var originsStr = Environment.GetEnvironmentVariable("CORS_ORIGINS") 
            ?? await settingsSvc.GetOrDefaultAsync("cors_origins", defaultOrigins) 
            ?? defaultOrigins;
        corsOrigins = originsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    // Security header sanity check — if an operator accidentally disabled CSP in Production,
    // surface it in the logs instead of silently serving requests with weakened policy.
    if (app.Environment.IsProduction())
    {
        var securityOpts = app.Services
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<SecurityHeadersOptions>>().Value;
        if (!securityOpts.EnableHstsAndCsp)
            Log.Warning("[Security] HSTS+CSP disabled in Production — set Security:Csp:EnableHstsAndCsp=true");
    }

    // Middleware pipeline
    // Must run before any middleware that reads RemoteIpAddress (rate limiting, audit logs).
    app.UseForwardedHeaders();

    app.UseResponseCompression();
    app.UseMiddleware<SecurityHeadersMiddleware>();

    // CORS must run before rate limiting so that 429 responses still include CORS headers.
    // Methods/headers are allow-listed rather than AllowAny* — we only ever serve the verbs
    // below, and narrowing the preflight response limits damage if a future cookie-auth
    // path is added without revisiting CORS. Retry-After is exposed so clients can read it
    // from 429 responses.
    app.UseCors(policy =>
    {
        policy.WithOrigins(corsOrigins)
              .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
              .WithHeaders("Authorization", "Content-Type", "Accept", "X-Requested-With", "Idempotency-Key")
              .WithExposedHeaders("Retry-After", "X-Correlation-Id")
              .AllowCredentials()
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<RateLimitingMiddleware>();
    app.UseMiddleware<IdempotencyMiddleware>();
    app.UseMiddleware<ErrorHandlingMiddleware>();

    // HTTPS redirect in production
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    // Static files for uploads
    var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
    Directory.CreateDirectory(uploadsPath);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
        RequestPath = "/uploads"
    });

    app.UseMiddleware<DeviceSessionMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<RoleAuthorizationMiddleware>();

    app.MapControllers();

    // OpenAPI + Scalar: only available in development
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    Log.Information("Event Platform API starting on port {Port}", port);
    await app.RunAsync();
}
catch (HostAbortedException)
{
    // Expected when dotnet-ef spins up the host to resolve the DbContext then aborts it — not an error
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
    var sslMode = Environment.GetEnvironmentVariable("DATABASE_SSL_MODE")
        ?? (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" ? "Disable" : "VerifyFull");

    // If it's already an Npgsql connection string (contains "Host="), use it directly
    if (url.Contains("Host=", StringComparison.OrdinalIgnoreCase))
    {
        // Ensure SslMode and pool settings are present
        if (!url.Contains("SslMode=", StringComparison.OrdinalIgnoreCase))
            url += $";SslMode={sslMode}";
        if (!url.Contains("Minimum Pool Size=", StringComparison.OrdinalIgnoreCase))
            url += ";Minimum Pool Size=10;Maximum Pool Size=100;Connection Idle Lifetime=300;Command Timeout=30;Timeout=15";
        return url;
    }

    // Otherwise parse as a postgres:// URI
    var uri = new Uri(url);
    var userInfo = uri.UserInfo.Split(':');
    var host = ResolveToIPv4(uri.Host);
    return $"Host={host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SslMode={sslMode};Minimum Pool Size=10;Maximum Pool Size=100;Connection Idle Lifetime=300;Command Timeout=30;Timeout=15";
}

/// <summary>
/// Resolves a hostname to an IPv4 address to avoid IPv6 connectivity issues in CI.
/// Falls back to the original hostname if resolution fails.
/// </summary>
static string ResolveToIPv4(string host)
{
    try
    {
        var addresses = System.Net.Dns.GetHostAddresses(host);
        var ipv4 = addresses.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
        if (ipv4 is not null) return ipv4.ToString();
    }
    catch { /* Fall back to hostname */ }
    return host;
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
    // Enable TLS for non-localhost connections (e.g., Upstash)
    if (!uri.Host.Contains("localhost") && !uri.Host.Contains("127.0.0.1"))
        config += ",ssl=true,abortConnect=false";
    return config;
}

/// <summary>
/// Reads the JWT secret from environment variables and configures the JWT bearer middleware.
/// </summary>
static Task ConfigureJwtSigningKey(WebApplication app)
{
    var secrets = app.Services.GetRequiredService<ISecretsProvider>();

    var jwtOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<JwtBearerOptions>>();
    var bearerOptions = jwtOptions.Get(JwtBearerDefaults.AuthenticationScheme);
    bearerOptions.TokenValidationParameters.IssuerSigningKey =
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secrets.JwtSecret));

    return Task.CompletedTask;
}
