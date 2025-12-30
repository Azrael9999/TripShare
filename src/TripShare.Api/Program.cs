using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using DbUp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Enrichers.CorrelationId;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;
using StackExchange.Redis;
using TripShare.Api.Middleware;
using TripShare.Api.HealthChecks;
using TripShare.Api.Services;
using TripShare.Application.Abstractions;
using TripShare.Infrastructure.Data;

LoadEnvFiles();
var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true)
    .AddUserSecrets<Program>(optional: true);

ValidateConfiguration(builder.Configuration, builder.Environment);

// Serilog
builder.Host.UseSerilog((ctx, lc) =>
{
    var aiConnectionString = ctx.Configuration["ApplicationInsights:ConnectionString"];
    var aiRoleName = ctx.Configuration["ApplicationInsights:RoleName"] ?? "HopTrip.Api";

    lc
        .Enrich.FromLogContext()
        .Enrich.WithCorrelationId()
        .Enrich.WithProperty("service", aiRoleName)
        .WriteTo.Console()
        .ReadFrom.Configuration(ctx.Configuration);

    if (ctx.HostingEnvironment.IsDevelopment())
    {
        lc.WriteTo.File("Logs/tripshare-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14);
    }

    if (!string.IsNullOrWhiteSpace(aiConnectionString))
    {
        lc.WriteTo.ApplicationInsights(
            telemetryConfiguration: new TelemetryConfiguration { ConnectionString = aiConnectionString },
            telemetryConverter: new TraceTelemetryConverter());
    }
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

builder.Services.AddControllers().AddJsonOptions(opt =>
{
    opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

        if (!builder.Environment.IsDevelopment() && origins.Length == 0)
        {
            throw new InvalidOperationException("Cors:AllowedOrigins must be configured in non-development environments.");
        }

        if (origins.Length == 0)
        {
            // Dev-friendly default: Vue dev server + Swagger UI usage
            policy
                .AllowAnyHeader()
                .AllowAnyMethod()
                .SetIsOriginAllowed(_ => true)
                .AllowCredentials();
        }
        else
        {
            policy
                .WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

// Telemetry
builder.Services.AddApplicationInsightsTelemetry(opt =>
{
    var connectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        opt.ConnectionString = connectionString;
    }
    opt.EnableAdaptiveSampling = true;
});

builder.Services.AddHealthChecks()
    .AddCheck<SqlConnectionHealthCheck>(
        "db",
        failureStatus: HealthStatus.Unhealthy);

builder.Services.AddSingleton<SqlConnectionHealthCheck>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection");
    return new SqlConnectionHealthCheck(connectionString, sp.GetRequiredService<ILogger<SqlConnectionHealthCheck>>());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient();

// Rate limiting for auth endpoints (simple baseline)
builder.Services.AddRateLimiter(opt =>
{
    opt.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    opt.AddPolicy("location-updates", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "location-unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    opt.AddPolicy("telemetry", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "telemetry-unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    opt.AddPolicy("verification", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "verify-unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(10),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

// EF Core
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"), sql =>
    {
        sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
    });
});

// Auth
var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = jwt["SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey missing");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

// App services
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IGoogleIdTokenValidator, GoogleIdTokenValidator>();
builder.Services.AddScoped<IEmailSender, EmailSenderFactory>();
builder.Services.AddSingleton<AcsSmsSender>();
builder.Services.AddSingleton<TextLkSmsSender>();
builder.Services.AddScoped<ISmsSender, SmsSenderRouter>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TripService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<RatingService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<BlockService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<SiteSettingsService>();
builder.Services.AddScoped<SafetyService>();
builder.Services.AddScoped<MessagingService>();
builder.Services.AddScoped<IdentityVerificationService>();
builder.Services.AddHostedService<BookingHousekeepingService>();
builder.Services.AddScoped<IBackgroundJobQueue, BackgroundJobQueue>();
builder.Services.AddScoped<BackgroundJobProcessor>();
builder.Services.AddHostedService<BackgroundJobHostedService>();
builder.Services.AddSingleton<TripLocationStreamService>();

// Distributed cache (Redis if configured, else memory)
var redisConnection = builder.Configuration["Cache:RedisConnection"];
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(opt => opt.Configuration = redisConnection);
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

// Background jobs provider
var jobsProvider = builder.Configuration["BackgroundJobs:Provider"] ?? "StorageQueue";
if (jobsProvider.Equals("StorageQueue", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<StorageQueueBackgroundJobQueue>();
    builder.Services.AddSingleton<IBackgroundJobQueue>(sp => sp.GetRequiredService<StorageQueueBackgroundJobQueue>());
    builder.Services.AddHostedService<StorageQueueBackgroundJobProcessor>();
}
else
{
    builder.Services.AddScoped<IBackgroundJobQueue, BackgroundJobQueue>();
    builder.Services.AddScoped<BackgroundJobProcessor>();
    builder.Services.AddHostedService<BackgroundJobHostedService>();
}

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("Default");

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false // liveness: just confirm the process is up
});
app.MapHealthChecks("/health/ready"); // readiness: include registered checks
app.MapHealthChecks("/health"); // backward compatibility

await EnsureDatabaseReadyAsync(app);
app.Run();

static async Task EnsureDatabaseReadyAsync(WebApplication app)
{
    // Production: use DbUp scripts for deterministic, idempotent schema setup.
    // Development: still supports EnsureCreated for quick local runs, but DbUp also runs if scripts exist.
    using var scope = app.Services.CreateScope();
    var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
    var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Db");

    var cs = cfg.GetConnectionString("DefaultConnection")
             ?? throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection");

    // Apply DbUp embedded scripts (recommended for production).
    try
    {
        var upgrader =
            DbUp.DeployChanges.To
                .SqlDatabase(cs)
                .WithExecutionTimeout(TimeSpan.FromMinutes(2))
                .LogToAutodetectedLog()
                .WithTransaction()
                .WithScriptsEmbeddedInAssembly(typeof(Program).Assembly, s => s.Contains(".DbUp.Scripts."))
                .Build();

        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
            throw result.Error!;

        log.LogInformation("Database schema is up to date (DbUp).");
    }
    catch (Exception ex)
    {
        // For dev environments where the scripts may not be embedded yet, allow a safe fallback.
        if (env.IsDevelopment())
        {
            log.LogWarning(ex, "DbUp upgrade failed in Development. Falling back to EnsureCreated.");
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            return;
        }

        throw;
    }
}

static void LoadEnvFiles()
{
    foreach (var path in new[] { ".env", ".env.local" })
    {
        if (!File.Exists(path)) continue;

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0) continue;

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            if (string.IsNullOrEmpty(key)) continue;

            Environment.SetEnvironmentVariable(key, value);
        }
    }
}

static void ValidateConfiguration(IConfiguration cfg, IHostEnvironment env)
{
    var errors = new List<string>();

    var connectionString = cfg.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        errors.Add("ConnectionStrings:DefaultConnection is required.");
    }

    var signingKey = cfg["Jwt:SigningKey"];
    if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
    {
        errors.Add("Jwt:SigningKey must be configured and not use the placeholder value.");
    }

    var allowedOrigins = cfg.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
    if (!env.IsDevelopment() && allowedOrigins.Length == 0)
    {
        errors.Add("Cors:AllowedOrigins must be set in non-development environments.");
    }

    if (errors.Count > 0)
    {
        throw new InvalidOperationException($"Configuration validation failed: {string.Join("; ", errors)}");
    }
}
