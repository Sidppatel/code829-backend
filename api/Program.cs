using System.Text;
using Api.Middleware;
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
using Microsoft.AspNetCore.DataProtection;
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

    var otlpEndpointRaw = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
    var otlpLogsEndpoint = string.IsNullOrWhiteSpace(otlpEndpointRaw) ? "http://localhost:4318" : otlpEndpointRaw;
    var otlpExporterEnabled = !string.IsNullOrWhiteSpace(otlpEndpointRaw)
        && !string.Equals(Environment.GetEnvironmentVariable("OTEL_SDK_DISABLED"), "true", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(Environment.GetEnvironmentVariable("OTEL_TRACES_EXPORTER"), "none", StringComparison.OrdinalIgnoreCase);
    var otlpHeadersRaw = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS");
    var otelServiceVersion = Environment.GetEnvironmentVariable("SENTRY_RELEASE")
        ?? Environment.GetEnvironmentVariable("RENDER_GIT_COMMIT")
        ?? "unknown";
    var isDevEnv = builder.Environment.IsDevelopment();

    var dbLoggingService = new DbLoggingService();
    builder.Services.AddSingleton<IDbLoggingService>(dbLoggingService);

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

        lc.WriteTo.Logger(lc2 => lc2
            .Filter.ByIncludingOnly(le => le.Level >= Serilog.Events.LogEventLevel.Warning)
            .WriteTo.Sink(new Api.Middleware.DbLoggingSink(dbLoggingService)));

        if (otlpExporterEnabled)
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

        }
    });

    var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = 15 * 1024 * 1024;
    });

    var connStr = BuildPostgresConnectionString();

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

    var redisConfig = BuildRedisConfigString();
    var redisOpts = StackExchange.Redis.ConfigurationOptions.Parse(redisConfig);
    Log.Information("[Redis] Connecting to {Endpoints} ssl={Ssl} user={User} abortConnect={Abort}",
        string.Join(",", redisOpts.EndPoints), redisOpts.Ssl, redisOpts.User ?? "<none>", redisOpts.AbortOnConnectFail);
    ConnectionMultiplexer redisMux;
    try
    {
        redisMux = ConnectionMultiplexer.Connect(redisOpts);
    }
    catch (Exception ex)
    {




        Log.Fatal(ex, "[Redis] Connect failed for {Endpoints} ssl={Ssl}",
            string.Join(",", redisOpts.EndPoints), redisOpts.Ssl);
        throw;
    }
    builder.Services.AddSingleton<IConnectionMultiplexer>(redisMux);

    builder.Services
        .AddDataProtection()
        .PersistKeysToStackExchangeRedis(redisMux, "ep:dataprotection-keys")
        .SetApplicationName("EventPlatform");

    builder.Services.AddHealthChecks()
        .AddNpgSql(connStr, name: "postgres")
        .AddRedis(redisConfig, name: "redis")
        .AddCheck<Api.HealthChecks.S3HealthCheck>("s3");

    builder.Services.AddSingleton<IEncryptionService, EncryptionService>();

    builder.Services.AddSingleton<ISecretsProvider, SecretsProvider>();

    builder.Services.Configure<SecurityHeadersOptions>(
        builder.Configuration.GetSection("Security:Csp"));

    var trustedProxies = Environment.GetEnvironmentVariable("TRUSTED_PROXIES");

    if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(trustedProxies))
        throw new InvalidOperationException(
            "TRUSTED_PROXIES must be set in non-Development environments");

    builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
        ForwardedHeadersConfig.Configure(
            options,
            builder.Environment.IsDevelopment(),
            trustedProxies));

    var stripeSecret = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY");
    if (string.IsNullOrEmpty(stripeSecret))
        throw new InvalidOperationException(
            "STRIPE_SECRET_KEY is required. Set test keys in .env for development or live keys in production — see .env.example.");
    if (builder.Environment.IsProduction() && !stripeSecret.StartsWith("sk_live_"))
        throw new InvalidOperationException(
            "Production environment requires a live Stripe secret key (sk_live_*). Refusing to start with test keys.");

    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IAppSettingRepository, AppSettingRepository>();
    builder.Services.AddScoped<ILogRepository, LogRepository>();

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
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.IPerformerProcedures, Db.Repositories.StoredProcedures.PerformerProcedures>();
    builder.Services.AddScoped<Db.Repositories.StoredProcedures.ISponsorProcedures, Db.Repositories.StoredProcedures.SponsorProcedures>();

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
    builder.Services.AddHttpClient();
    builder.Services.AddScoped<IImageService, ImageService>();
    builder.Services.AddScoped<IEventImageService, EventImageService>();
    builder.Services.AddScoped<IVenueImageService, VenueImageService>();
    builder.Services.AddScoped<IPlatformImageService, PlatformImageService>();
    builder.Services.AddScoped<IPerformerService, PerformerService>();
    builder.Services.AddScoped<ISponsorService, SponsorService>();
    builder.Services.AddScoped<ICacheService, RedisCacheService>();
    builder.Services.AddScoped<IFinancialService, FinancialService>();

    if (!string.IsNullOrEmpty(builder.Configuration["CLAMAV_HOST"]))
        builder.Services.AddSingleton<IMalwareScanner, ClamAvScanner>();
    else
        builder.Services.AddSingleton<IMalwareScanner, NoopMalwareScanner>();

    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
    }
    else
    {

        var s3Access = Environment.GetEnvironmentVariable("S3_ACCESS_KEY");
        var s3Secret = Environment.GetEnvironmentVariable("S3_SECRET_KEY");
        var s3Bucket = Environment.GetEnvironmentVariable("S3_BUCKET");
        if (string.IsNullOrEmpty(s3Access) || string.IsNullOrEmpty(s3Secret) || string.IsNullOrEmpty(s3Bucket))
            throw new InvalidOperationException(
                "S3_ACCESS_KEY, S3_SECRET_KEY, and S3_BUCKET are required outside Development. See .env.example.");

        builder.Services.AddSingleton<IFileStorageService, S3FileStorageService>();
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

    builder.Services.AddScoped<IPaymentEnrichmentService, PaymentEnrichmentService>();

    builder.Services.AddSingleton<IStripeConnectService, StripeConnectService>();

    builder.Services.AddSingleton<IAlertService, NoOpAlertService>();

    builder.Services.AddScoped<IOrganizationService, OrganizationService>();

    builder.Services.AddHostedService<LogCleanupWorker>();
    builder.Services.AddHostedService<HoldCleanupWorker>();
    builder.Services.AddHostedService<ScheduledPublishWorker>();
    builder.Services.AddHostedService<DbLoggingWorker>();

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

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = _ => Task.CompletedTask
            };
        });
    builder.Services.AddAuthorization();

    var otlpBase = otlpLogsEndpoint.TrimEnd('/');
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService("code829-api", serviceVersion: otelServiceVersion))
        .WithTracing(t =>
        {
            t.AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddRedisInstrumentation();
            if (otlpExporterEnabled)
                t.AddOtlpExporter(o =>
                {
                    o.Endpoint = new Uri($"{otlpBase}/v1/traces");
                    o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                    if (!string.IsNullOrWhiteSpace(otlpHeadersRaw)) o.Headers = otlpHeadersRaw;
                });
        })
        .WithMetrics(m =>
        {
            m.AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation();
            if (otlpExporterEnabled)
                m.AddOtlpExporter(o =>
                {
                    o.Endpoint = new Uri($"{otlpBase}/v1/metrics");
                    o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                    if (!string.IsNullOrWhiteSpace(otlpHeadersRaw)) o.Headers = otlpHeadersRaw;
                });
        });

    builder.Services.AddControllers(options =>
        {

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
    builder.Services.AddScoped<IValidator<SetPasswordRequest>, SetPasswordRequestValidator>();
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

    builder.Services.AddCors();

    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
        options.Providers.Add<BrotliCompressionProvider>();
        options.Providers.Add<GzipCompressionProvider>();
    });
    builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
    builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

    var app = builder.Build();





    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventPlatformDbContext>();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" DESC LIMIT 1;";
        string? latest;
        try
        {
            latest = (await cmd.ExecuteScalarAsync()) as string;
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P01")
        {
            throw new InvalidOperationException(
                "Database schema not initialized: __EFMigrationsHistory table missing. " +
                "Run the migrate-prod workflow before starting the API.", ex);
        }
        if (string.IsNullOrEmpty(latest))
            throw new InvalidOperationException(
                "Database schema not initialized: __EFMigrationsHistory is empty.");

        var expected = Environment.GetEnvironmentVariable("EXPECTED_MIGRATION_ID");
        if (!string.IsNullOrEmpty(expected) && !string.Equals(expected, latest, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"DB schema version mismatch. Expected '{expected}' but found '{latest}'. " +
                "Run migrate-prod against this database before deploying this backend version.");

        Log.Information("[DB] Schema version: {MigrationId} (expected: {Expected})",
            latest, string.IsNullOrEmpty(expected) ? "<presence-only>" : expected);
    }

    await ConfigureJwtSigningKey(app);

    string[] corsOrigins;
    {
        using var scope = app.Services.CreateScope();
        var settingsSvc = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var defaultOrigins = app.Environment.IsDevelopment()
            ? "http://localhost:5173,http://localhost:5174,http://localhost:5175,http://localhost:5176"
            : "http://localhost:5173";
        var originsStr = Environment.GetEnvironmentVariable("CORS_ORIGINS")
            ?? await settingsSvc.GetOrDefaultAsync("cors_origins", defaultOrigins)
            ?? defaultOrigins;
        corsOrigins = originsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    if (app.Environment.IsProduction())
    {
        var securityOpts = app.Services
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<SecurityHeadersOptions>>().Value;
        if (!securityOpts.EnableHstsAndCsp)
            Log.Warning("[Security] HSTS+CSP disabled in Production — set Security:Csp:EnableHstsAndCsp=true");
    }

    app.UseMiddleware<CloudflareIpAllowlistMiddleware>();

    app.UseForwardedHeaders();

    app.UseResponseCompression();
    app.UseMiddleware<SecurityHeadersMiddleware>();

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

    app.UseMiddleware<LegacyApiRedirectMiddleware>();
    app.UseMiddleware<RateLimitingMiddleware>();
    app.UseMiddleware<IdempotencyMiddleware>();
    app.UseMiddleware<ErrorHandlingMiddleware>();

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

    app.MapOpenApi().AddEndpointFilter(async (ctx, next) =>
    {
        if (app.Environment.IsDevelopment())
        {
            return await next(ctx);
        }

        var expected = app.Configuration["OPENAPI_PUBLIC_TOKEN"];
        if (string.IsNullOrEmpty(expected))
        {
            return Results.NotFound();
        }

        var auth = ctx.HttpContext.Request.Headers.Authorization.ToString();
        if (!string.Equals(auth, $"Bearer {expected}", StringComparison.Ordinal))
        {
            return Results.NotFound();
        }

        return await next(ctx);
    });

    if (app.Environment.IsDevelopment())
    {
        app.MapScalarApiReference();
    }

    Log.Information("Event Platform API starting on port {Port}", port);
    await app.RunAsync();
}
catch (HostAbortedException)
{

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

static string BuildPostgresConnectionString()
{
    var host = RequireEnv("DB_HOST");
    var port = RequireEnv("DB_PORT");
    var user = RequireEnv("DB_USER");
    var name = RequireEnv("DB_NAME");
    var password = RequireEnv("DB_PASSWORD");

    var sslMode = Environment.GetEnvironmentVariable("DATABASE_SSL_MODE")
        ?? (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" ? "Disable" : "VerifyFull");

    var minPool = Environment.GetEnvironmentVariable("DB_MIN_POOL") ?? "1";
    var maxPool = Environment.GetEnvironmentVariable("DB_MAX_POOL") ?? "20";
    var idleLifetime = Environment.GetEnvironmentVariable("DB_IDLE_LIFETIME") ?? "60";
    var cmdTimeout = Environment.GetEnvironmentVariable("DB_CMD_TIMEOUT") ?? "30";
    var connTimeout = Environment.GetEnvironmentVariable("DB_CONN_TIMEOUT") ?? "15";

    var resolvedHost = ResolveToIPv4(host);
    return $"Host={resolvedHost};Port={port};Database={name};Username={user};Password={password}" +
           $";SslMode={sslMode}" +
           $";Minimum Pool Size={minPool};Maximum Pool Size={maxPool}" +
           $";Connection Idle Lifetime={idleLifetime}" +
           $";Command Timeout={cmdTimeout};Timeout={connTimeout}" +
           ";No Reset On Close=true";
}

static string RequireEnv(string key)
{
    var v = Environment.GetEnvironmentVariable(key);
    if (string.IsNullOrEmpty(v))
        throw new InvalidOperationException($"{key} is required");
    return v;
}

static string ResolveToIPv4(string host)
{
    try
    {
        var addresses = System.Net.Dns.GetHostAddresses(host);
        var ipv4 = addresses.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
        if (ipv4 is not null) return ipv4.ToString();
    }
    catch { }
    return host;
}

static string BuildRedisConfigString()
{
    var host = RequireEnv("REDIS_HOST");
    var port = RequireEnv("REDIS_PORT");
    var user = Environment.GetEnvironmentVariable("REDIS_USER");
    var password = Environment.GetEnvironmentVariable("REDIS_PASSWORD");
    var tls = string.Equals(Environment.GetEnvironmentVariable("REDIS_TLS"), "true",
        StringComparison.OrdinalIgnoreCase);

    var config = $"{host}:{port}";
    if (!string.IsNullOrEmpty(user))
        config += $",user={user}";
    if (!string.IsNullOrEmpty(password))
        config += $",password={password}";
    if (tls)
        config += ",ssl=true";

    var isLoopback = host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host == "127.0.0.1"
        || host == "::1";
    if (!isLoopback)
        config += ",abortConnect=false,connectTimeout=5000,syncTimeout=5000";

    return config;
}

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
