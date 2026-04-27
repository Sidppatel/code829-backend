using System.Text;
using Api.Middleware;
using Api.Seeding;
using Api.Services;
using Api.Validators;
using Api.Workers;
using Contracts.DTOs.Auth;
using Contracts.DTOs.Purchases;
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
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
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
    // Bootstrap precedence (LATER overrides EARLIER):
    //   1. Process env (from Infisical via start-backend.ps1, or whatever
    //      the parent shell injected when running `dotnet run` manually).
    //   2. .env (Infisical-style snapshot, optional fallback for IDE runs
    //      that bypass the start script).
    //   3. .env.local (committed-grade, monorepo-root, gitignored — local
    //      dev overrides like docker creds, dev URLs, and a stable
    //      STRIPE_WEBHOOK_SECRET pinned to a `stripe listen` session).
    // .env.local is loaded LAST so dev overrides win over Infisical
    // snapshots, matching the order in start-backend.ps1.
    var envFiles = new[]
    {
        Path.Combine(Directory.GetCurrentDirectory(), "..", ".env"),
        Path.Combine(Directory.GetCurrentDirectory(), ".env"),
        Path.Combine(Directory.GetCurrentDirectory(), "..", ".env.local"),
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env.local"),
        Path.Combine(Directory.GetCurrentDirectory(), ".env.local"),
    };
    foreach (var envPath in envFiles)
    {
        if (!File.Exists(envPath)) continue;
        Console.WriteLine($"[Bootstrap] Loading environment from {envPath}");
        foreach (var line in File.ReadAllLines(envPath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;
            var eqIndex = trimmed.IndexOf('=');
            if (eqIndex <= 0) continue;
            var key = trimmed[..eqIndex].Trim();
            var value = trimmed[(eqIndex + 1)..].Trim();
            // Strip surrounding single or double quotes — Infisical exports
            // some secret values quoted; raw values from .env.local are not.
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}

var builder = WebApplication.CreateBuilder(args);

    // Sentry — capture unhandled 5xx exceptions only. Empty-string DSN disables transport
    // (per Sentry docs) so local dev without a configured DSN boots cleanly.
    var sentryDsn = Environment.GetEnvironmentVariable("SENTRY_DSN") ?? string.Empty;
    builder.WebHost.UseSentry(o =>
    {
        o.Dsn = sentryDsn;
        o.Environment = builder.Environment.EnvironmentName;
        o.Release = Environment.GetEnvironmentVariable("SENTRY_RELEASE")
            ?? Environment.GetEnvironmentVariable("RENDER_GIT_COMMIT")
            ?? typeof(Program).Assembly.GetName().Version?.ToString()
            ?? "unknown";
        o.TracesSampleRate = 0.1;
        o.SendDefaultPii = false;
        o.MaxBreadcrumbs = 50;
    });

    if (string.IsNullOrEmpty(sentryDsn) && builder.Environment.IsProduction())
        Log.Warning("[Sentry] DSN not configured — telemetry disabled");

    // Serilog — structured logging. Development: console + rolling files for offline triage.
    // Production: console + OTLP sink (logs shipped to Grafana Cloud, unified with OTEL traces
    // via TraceId enricher). Render's filesystem is ephemeral, so file sinks are Development-only
    // (BE #12).
    var otlpLogsEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
        ?? "http://localhost:4318";
    var otlpHeadersRaw = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS");
    var otelServiceVersion = Environment.GetEnvironmentVariable("SENTRY_RELEASE")
        ?? Environment.GetEnvironmentVariable("RENDER_GIT_COMMIT")
        ?? "unknown";
    var isDevEnv = builder.Environment.IsDevelopment();

    builder.Host.UseSerilog((ctx, lc) =>
    {
        lc.ReadFrom.Configuration(ctx.Configuration)
          .Enrich.WithMachineName()
          .Enrich.FromLogContext()
          .Enrich.With<Api.Middleware.OpenTelemetryTraceEnricher>()
          .MinimumLevel.Information()
          .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
          .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning);

        lc.WriteTo.Console(
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{CorrelationId}] [trace={TraceId}] {Message:lj}{NewLine}{Exception}");

        lc.WriteTo.OpenTelemetry(o =>
        {
            o.Endpoint = $"{otlpLogsEndpoint.TrimEnd('/')}/v1/logs";
            o.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.HttpProtobuf;
            o.ResourceAttributes = new Dictionary<string, object>
            {
                ["service.name"] = "code829-api",
                ["service.version"] = otelServiceVersion,
                ["deployment.environment"] = ctx.HostingEnvironment.EnvironmentName,
            };
            if (!string.IsNullOrWhiteSpace(otlpHeadersRaw))
            {
                var headers = new Dictionary<string, string>();
                foreach (var pair in otlpHeadersRaw.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var eq = pair.IndexOf('=');
                    if (eq <= 0) continue;
                    headers[pair[..eq].Trim()] = pair[(eq + 1)..].Trim();
                }
                o.Headers = headers;
            }
        });

        if (isDevEnv)
        {
            lc.WriteTo.File("logs/api-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{CorrelationId}] [{UserId}] [trace={TraceId}] {SourceContext}{NewLine}  {Message:lj}{NewLine}{Exception}");

            lc.WriteTo.Logger(lc2 => lc2
                .Filter.ByIncludingOnly(le => le.Level >= Serilog.Events.LogEventLevel.Error)
                .WriteTo.File("logs/errors-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{CorrelationId}] [{UserId}] [trace={TraceId}] {SourceContext}{NewLine}  {Message:lj}{NewLine}{Exception}"));

            lc.WriteTo.Logger(lc2 => lc2
                .Filter.ByIncludingOnly(le => le.MessageTemplate.Text.Contains("[Seed]"))
                .WriteTo.File("logs/seeding-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} {Message:lj}{NewLine}{Exception}"));
        }
    });

    // Kestrel on configurable port with request size limits.
    // Default 10000 matches the Dockerfile + render.yaml contract. Local dev
    // scripts set PORT=8000 in .env.local to override.
    var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = 15 * 1024 * 1024; // 15 MB global limit
    });

    // Database
    var dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
        ?? throw new InvalidOperationException("DATABASE_URL is required");
    var connStr = ConvertPostgresUrl(dbUrl);

    // Surface the parsed host:port at startup so prod can confirm which pooler
    // tier is in use without leaking creds. Crucial when chasing 5432 vs 6543
    // mismatches between Render env and what the runtime actually loads.
    try
    {
        var csb = new Npgsql.NpgsqlConnectionStringBuilder(connStr);
        Log.Information("[DB] Connecting to {Host}:{Port} db={Database} pool={Min}-{Max} sslmode={Ssl}",
            csb.Host, csb.Port, csb.Database, csb.MinPoolSize, csb.MaxPoolSize, csb.SslMode);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "[DB] Failed to parse connection string for startup log");
    }

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

    // Health checks — /health/ready (deep), /health/live stays in HealthController
    builder.Services.AddHealthChecks()
        .AddNpgSql(connStr, name: "postgres")
        .AddRedis(redisConfig, name: "redis")
        .AddCheck<Api.HealthChecks.S3HealthCheck>("s3");

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
    var trustedProxies = Environment.GetEnvironmentVariable("TRUSTED_PROXIES");

    // BE #102 — refuse to start outside Development without TRUSTED_PROXIES set. Without
    // it, ForwardedHeadersConfig trusts nothing and every client appears to come from the
    // platform load balancer, breaking per-IP rate limits and audit attribution.
    if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(trustedProxies))
        throw new InvalidOperationException(
            "TRUSTED_PROXIES must be set in non-Development environments");

    builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
        ForwardedHeadersConfig.Configure(
            options,
            builder.Environment.IsDevelopment(),
            trustedProxies));

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
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IPurchaseProcedures, Db.Repositories.StoredProcedures.PurchaseProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.ITicketProcedures, Db.Repositories.StoredProcedures.TicketProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IStripeTransactionProcedures, Db.Repositories.StoredProcedures.StripeTransactionProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IImageProcedures, Db.Repositories.StoredProcedures.ImageProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IEventImageProcedures, Db.Repositories.StoredProcedures.EventImageProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IVenueImageProcedures, Db.Repositories.StoredProcedures.VenueImageProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IPlatformImageProcedures, Db.Repositories.StoredProcedures.PlatformImageProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.ISettingsProcedures, Db.Repositories.StoredProcedures.SettingsProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.ILogProcedures, Db.Repositories.StoredProcedures.LogProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IDashboardProcedures, Db.Repositories.StoredProcedures.DashboardProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IFeedbackProcedures, Db.Repositories.StoredProcedures.FeedbackProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IEventTicketTypeProcedures, Db.Repositories.StoredProcedures.EventTicketTypeProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IBusinessUserProcedures, Db.Repositories.StoredProcedures.BusinessUserProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IInvitationProcedures, Db.Repositories.StoredProcedures.InvitationProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.ILayoutProcedures, Db.Repositories.StoredProcedures.LayoutProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IBusinessPasswordResetTokenProcedures, Db.Repositories.StoredProcedures.BusinessPasswordResetTokenProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.ICheckInProcedures, Db.Repositories.StoredProcedures.CheckInProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IBusinessUserEventProcedures, Db.Repositories.StoredProcedures.BusinessUserEventProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IOrganizationProcedures, Db.Repositories.StoredProcedures.OrganizationProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IStripeEventProcedures, Db.Repositories.StoredProcedures.StripeEventProcedures>();

    // Services
    builder.Services.AddScoped<ISettingsService, SettingsService>();
    builder.Services.AddScoped<IJwtService, JwtService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IAdminAuthService, AdminAuthService>();
    builder.Services.AddScoped<IInvitationService, InvitationService>();
    builder.Services.AddScoped<ITableBookingService, TableBookingService>();
    builder.Services.AddScoped<IPurchaseService, PurchaseService>();
    builder.Services.AddScoped<IAuditLogService, AuditLogService>();
    builder.Services.AddScoped<IAdminLogService, AdminLogService>();
    builder.Services.AddScoped<IImageRepository, ImageRepository>();
    builder.Services.AddScoped<IImageProcessingService, ImageProcessingService>();
    builder.Services.AddScoped<IImageService, ImageService>();
    builder.Services.AddScoped<IEventImageService, EventImageService>();
    builder.Services.AddScoped<IVenueImageService, VenueImageService>();
    builder.Services.AddScoped<IPlatformImageService, PlatformImageService>();
    builder.Services.AddScoped<ICacheService, RedisCacheService>();
    builder.Services.AddScoped<IFinancialService, FinancialService>();

    // Malware scanner: ClamAV when CLAMAV_HOST is set, no-op otherwise (local dev convenience).
    // Production must set CLAMAV_HOST — see docs/runbooks/secret-rotation.md is not the place; deployment env config is.
    if (!string.IsNullOrEmpty(builder.Configuration["CLAMAV_HOST"]))
        builder.Services.AddSingleton<IMalwareScanner, ClamAvScanner>();
    else
        builder.Services.AddSingleton<IMalwareScanner, NoopMalwareScanner>();

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
    // Shared post-confirmation enrich + tax-record path. Used by both the
    // Stripe webhook handler AND PurchaseService.ConfirmAsync so dev (no
    // public webhook URL) and prod (webhook fires) both land the data.
    builder.Services.AddScoped<IPaymentEnrichmentService, PaymentEnrichmentService>();

    // Stripe Connect — Express account creation, onboarding-link generation,
    // status fetch. Singleton because it depends on ISecretsProvider (also
    // singleton); the Stripe key is read on every call so env-var rotation
    // takes effect on the next request without restart of this service.
    builder.Services.AddSingleton<IStripeConnectService, StripeConnectService>();

    // Alert fan-out abstraction. NoOp impl logs at Error severity via Serilog;
    // production swaps in Sentry / Resend / PagerDuty without callers changing.
    builder.Services.AddSingleton<IAlertService, NoOpAlertService>();

    // Organization CRUD + membership service. Scoped because it consumes the
    // scoped IOrganizationProcedures (which holds the EF DbContext).
    builder.Services.AddScoped<IOrganizationService, OrganizationService>();

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
                ValidIssuer = JwtConstants.Issuer,
                ValidAudience = JwtConstants.Audience,
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

    // OpenTelemetry — traces + metrics via OTLP. Endpoint defaults to local Jaeger
    // (docker-compose.observability.yml). Prod points at Grafana Cloud OTLP endpoint
    // via OTEL_EXPORTER_OTLP_ENDPOINT + OTEL_EXPORTER_OTLP_HEADERS (basic auth token).
    var otlpBase = otlpLogsEndpoint.TrimEnd('/');
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService("code829-api", serviceVersion: otelServiceVersion))
        .WithTracing(t => t
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddRedisInstrumentation()
            .AddOtlpExporter(o =>
            {
                o.Endpoint = new Uri($"{otlpBase}/v1/traces");
                o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                if (!string.IsNullOrWhiteSpace(otlpHeadersRaw)) o.Headers = otlpHeadersRaw;
            }))
        .WithMetrics(m => m
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(o =>
            {
                o.Endpoint = new Uri($"{otlpBase}/v1/metrics");
                o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                if (!string.IsNullOrWhiteSpace(otlpHeadersRaw)) o.Headers = otlpHeadersRaw;
            }));

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
        // URL-segment reader: clients call /v1/events. Header/query readers
        // are intentionally omitted — we want one canonical shape.
        options.ApiVersionReader = new Asp.Versioning.UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
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
    builder.Services.AddScoped<IValidator<CreatePurchaseRequest>, CreatePurchaseRequestValidator>();
    builder.Services.AddScoped<IValidator<CreateEventRequest>, CreateEventRequestValidator>();
    builder.Services.AddScoped<IValidator<SignupRequest>, SignupRequestValidator>();
    builder.Services.AddScoped<IValidator<SigninRequest>, SigninRequestValidator>();
    builder.Services.AddScoped<IValidator<ForgotPasswordRequest>, ForgotPasswordRequestValidator>();
    builder.Services.AddScoped<IValidator<ResetPasswordRequest>, ResetPasswordRequestValidator>();
    builder.Services.AddScoped<IValidator<VerifyEmailRequest>, VerifyEmailRequestValidator>();
    builder.Services.AddScoped<IValidator<Contracts.DTOs.Organizations.OrganizationCreateRequest>, OrganizationCreateRequestValidator>();
    builder.Services.AddScoped<IValidator<Contracts.DTOs.Organizations.OrganizationUpdateRequest>, OrganizationUpdateRequestValidator>();
    builder.Services.AddScoped<IValidator<Contracts.DTOs.Organizations.OrganizationMemberRequest>, OrganizationMemberRequestValidator>();
    builder.Services.AddScoped<IValidator<Contracts.DTOs.Organizations.StripeOnboardingLinkRequest>, StripeOnboardingLinkRequestValidator>();
    builder.Services.AddScoped<IValidator<Contracts.DTOs.Organizations.StartStripeOnboardingRequest>, StartStripeOnboardingRequestValidator>();
    builder.Services.AddScoped<IValidator<Contracts.DTOs.Layout.SaveLayoutRequest>, SaveLayoutRequestValidator>();
    builder.Services.AddScoped<IValidator<Contracts.DTOs.Layout.AddTableRequest>, AddTableRequestValidator>();
    builder.Services.AddScoped<IValidator<Contracts.DTOs.Layout.UpdateTableRequest>, UpdateTableRequestValidator>();
    builder.Services.AddScoped<IValidator<Contracts.DTOs.Layout.CreateTableTemplateRequest>, CreateTableTemplateRequestValidator>();
    builder.Services.AddScoped<IValidator<PricingQuoteRequest>, PricingQuoteRequestValidator>();

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

    // One-shot prod bootstrap: RUN_PROD_BOOTSTRAP=true + Production environment.
    // Runs migrations + seeds default AppSettings + initial developer user, logs result,
    // then exits 0 so the server never starts on a bootstrap boot. See docs/runbooks/prod-bootstrap.md.
    if (app.Environment.IsProduction()
        && string.Equals(Environment.GetEnvironmentVariable("RUN_PROD_BOOTSTRAP"), "true", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            await Api.Seeding.ProdBootstrap.RunAsync(app.Services);
            Log.Information("[ProdBootstrap] Exiting 0 — unset RUN_PROD_BOOTSTRAP and redeploy to start server");
            await Log.CloseAndFlushAsync();
            Environment.Exit(0);
            return;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "[ProdBootstrap] Failed");
            await Log.CloseAndFlushAsync();
            Environment.Exit(1);
            return;
        }
    }

    // Apply pending migrations on every startup — with retry for slow DB startup.
    // Set SKIP_MIGRATIONS=true to bypass for schema-validation / read-only boot.
    var skipMigrations = string.Equals(
        Environment.GetEnvironmentVariable("SKIP_MIGRATIONS"), "true", StringComparison.OrdinalIgnoreCase);
    const int maxRetries = 5;
    for (var attempt = 1; attempt <= maxRetries && !skipMigrations; attempt++)
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

    // Seed data — only in Development. The historical SEED_DATA override is
    // IGNORED in non-Development environments because the admin seeders plant
    // accounts with well-known default passwords. Use ProdBootstrap for real
    // deploys. Log the misconfiguration loudly but do not crash — the seeding
    // branch below is independently gated by IsDevelopment(), so no unsafe
    // seed can occur regardless of this flag's value in prod.
    if (Environment.GetEnvironmentVariable("SEED_DATA")?.ToLower() == "true"
        && !app.Environment.IsDevelopment())
    {
        Log.Warning(
            "SEED_DATA=true set in non-Development environment (ASPNETCORE_ENVIRONMENT={Env}). " +
            "Ignoring — default-password seeding is never executed outside Development. " +
            "Unset SEED_DATA in your deploy environment to silence this warning.",
            app.Environment.EnvironmentName);
    }

    if (skipMigrations)
    {
        Log.Warning("SKIP_MIGRATIONS=true — bypassed db.Database.MigrateAsync() and all seeders");
    }
    else if (app.Environment.IsDevelopment())
    {
        // Opt-out via SEED_USERS_AND_PURCHASES=false: skips the regular-user
        // seed (8 default ticket buyers) and the synthetic purchase seed
        // (~135 rows). Useful when you want a clean stage with only events
        // and your custom business_users (e.g. for manual onboarding tests).
        // Defaults to true — preserves the standard dev experience.
        var seedUsersAndPurchases =
            (Environment.GetEnvironmentVariable("SEED_USERS_AND_PURCHASES") ?? "true")
            .Equals("true", StringComparison.OrdinalIgnoreCase);

        await DataSeeder.SeedAsync(app.Services, seedUsers: seedUsersAndPurchases);
        await VenueEventSeeder.SeedAsync(app.Services);
        await LayoutSeeder.SeedAsync(app.Services);
        if (seedUsersAndPurchases)
        {
            await PurchaseSeeder.SeedAsync(app.Services);
        }
        else
        {
            Log.Information("[Seed] SEED_USERS_AND_PURCHASES=false — skipped user + purchase seeding");
        }
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
    // CloudflareIpAllowlistMiddleware runs before UseForwardedHeaders so it
    // inspects the raw TCP peer, not the X-Forwarded-For client IP. Gated
    // behind CF_IP_ALLOWLIST_ENFORCE to avoid rejecting Render's internal LB
    // until the service is fully fronted by Cloudflare.
    app.UseMiddleware<CloudflareIpAllowlistMiddleware>();
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
              .WithHeaders("Authorization", "Content-Type", "Accept", "X-Requested-With", "Idempotency-Key", "x-portal")
              .WithExposedHeaders("Retry-After", "X-Correlation-Id")
              .AllowCredentials()
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });

    app.UseMiddleware<CorrelationIdMiddleware>();
    // Redirect legacy unversioned /foo → /v1/foo (301). 90-day grace window
    // from 2026-04-23; remove after 2026-07-22 once FE baseURL + any remaining
    // clients are confirmed updated. See api/Middleware/LegacyApiRedirectMiddleware.cs.
    app.UseMiddleware<LegacyApiRedirectMiddleware>();
    app.UseMiddleware<RateLimitingMiddleware>();
    app.UseMiddleware<IdempotencyMiddleware>();
    app.UseMiddleware<ErrorHandlingMiddleware>();

    // HTTPS redirect in production
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    // Static files for uploads — Development only. Prod uses S3FileStorageService
    // exclusively and does not write a writable `uploads/` dir to the filesystem
    // (see Dockerfile runtime-stage comment). Attempting Directory.CreateDirectory
    // against the read-only prod image crashes startup.
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

    app.UseMiddleware<DeviceSessionMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<RoleAuthorizationMiddleware>();

    app.MapControllers();
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (ctx, report) =>
        {
            ctx.Response.ContentType = "application/json";
            var result = System.Text.Json.JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                timestamp = DateTime.UtcNow,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                }),
            });
            await ctx.Response.WriteAsync(result);
        },
    });

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
///
/// Pool defaults are tuned for Supabase's pgbouncer transaction pooler (port 6543)
/// where keeping many idle sessions is harmful — pgbouncer holds slots open per
/// idle Npgsql connection and quickly exhausts the project quota across replicas.
/// Override per-env via DB_MIN_POOL / DB_MAX_POOL / DB_IDLE_LIFETIME / DB_CMD_TIMEOUT
/// / DB_CONN_TIMEOUT to tune for direct or session-pooler connections.
///
/// `No Reset On Close=true` is required when talking to pgbouncer in transaction
/// mode because `DISCARD ALL` is not supported across pooled connections.
/// `Max Auto Prepare=0` (Npgsql default) disables auto-prepare which pgbouncer's
/// transaction pooler does not preserve between transactions.
/// </summary>
static string ConvertPostgresUrl(string url)
{
    var sslMode = Environment.GetEnvironmentVariable("DATABASE_SSL_MODE")
        ?? (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" ? "Disable" : "VerifyFull");

    var minPool = Environment.GetEnvironmentVariable("DB_MIN_POOL") ?? "1";
    var maxPool = Environment.GetEnvironmentVariable("DB_MAX_POOL") ?? "20";
    var idleLifetime = Environment.GetEnvironmentVariable("DB_IDLE_LIFETIME") ?? "60";
    var cmdTimeout = Environment.GetEnvironmentVariable("DB_CMD_TIMEOUT") ?? "30";
    var connTimeout = Environment.GetEnvironmentVariable("DB_CONN_TIMEOUT") ?? "15";

    var poolSuffix =
        $";Minimum Pool Size={minPool};Maximum Pool Size={maxPool}" +
        $";Connection Idle Lifetime={idleLifetime}" +
        $";Command Timeout={cmdTimeout};Timeout={connTimeout}" +
        ";No Reset On Close=true";

    // If it's already an Npgsql connection string (contains "Host="), use it directly
    if (url.Contains("Host=", StringComparison.OrdinalIgnoreCase))
    {
        // Ensure SslMode and pool settings are present
        if (!url.Contains("SslMode=", StringComparison.OrdinalIgnoreCase))
            url += $";SslMode={sslMode}";
        if (!url.Contains("Minimum Pool Size=", StringComparison.OrdinalIgnoreCase))
            url += poolSuffix;
        return url;
    }

    // Otherwise parse as a postgres:// URI
    var uri = new Uri(url);
    var userInfo = uri.UserInfo.Split(':');
    if (userInfo.Length < 2)
        throw new InvalidOperationException("DATABASE_URL missing user:password");
    var host = ResolveToIPv4(uri.Host);
    return $"Host={host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SslMode={sslMode}{poolSuffix}";
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

    var keys = new List<SecurityKey>
    {
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secrets.JwtSecret))
    };
    var previous = secrets.JwtSecretPrevious;
    if (!string.IsNullOrEmpty(previous))
        keys.Add(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(previous)));

    bearerOptions.TokenValidationParameters.IssuerSigningKey = keys[0];
    bearerOptions.TokenValidationParameters.IssuerSigningKeys = keys;

    return Task.CompletedTask;
}
