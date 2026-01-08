using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Azure;
using Azure.Storage.Queues;
using DbUp;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using ILogger = Microsoft.Extensions.Logging.ILogger;
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

AppDomain.CurrentDomain.UnhandledException += (_, args) =>
{
    if (args.ExceptionObject is Exception ex)
    {
        Log.Fatal(ex, "Unhandled exception");
    }
    else
    {
        Log.Fatal("Unhandled exception: {Exception}", args.ExceptionObject);
    }

    Log.CloseAndFlush();
};

TaskScheduler.UnobservedTaskException += (_, args) =>
{
    Log.Error(args.Exception, "Unobserved task exception");
    args.SetObserved();
};

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
ConfigureTelemetry(builder.Services, builder.Configuration);

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
        sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
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
builder.Services.AddSingleton<DevFileSmsSender>();
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
builder.Services.AddSingleton<TripLocationStreamService>();

// Distributed cache (Redis if configured, else memory)
var redisConnection = ShouldUsePaidAzure(builder.Configuration)
    ? builder.Configuration["Cache:RedisConnection"]
    : null;
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
var storageQueueFallbackReason = default(string?);

if (jobsProvider.Equals("StorageQueue", StringComparison.OrdinalIgnoreCase))
{
    if (TryValidateStorageQueue(builder.Configuration, out storageQueueFallbackReason))
    {
        RegisterStorageQueueBackgroundJobs(builder.Services);
    }
    else if (builder.Environment.IsDevelopment())
    {
        jobsProvider = "Sql";
        RegisterSqlBackgroundJobs(builder.Services);
    }
    else
    {
        throw new InvalidOperationException($"Storage queue background jobs are enabled but unavailable: {storageQueueFallbackReason}");
    }
}
else
{
    RegisterSqlBackgroundJobs(builder.Services);
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

app.Lifetime.ApplicationStopping.Register(() =>
{
    Log.Warning("Application stopping.");
    Log.CloseAndFlush();
});

if (storageQueueFallbackReason is not null)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning("Storage queue provider disabled: {Reason}. Falling back to SQL background jobs. Start Azurite (UseDevelopmentStorage=true) or set BackgroundJobs__Provider=Sql for local runs.", storageQueueFallbackReason);
}

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

    var autoCreate = cfg.GetValue("Database:AutoCreate", env.IsDevelopment());
    if (autoCreate)
    {
        await EnsureDatabaseAndUserAsync(cs, log, env);
    }

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

static async Task EnsureDatabaseAndUserAsync(string connectionString, ILogger log, IHostEnvironment env)
{
    var builder = new SqlConnectionStringBuilder(connectionString);

    if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
    {
        throw new InvalidOperationException("Connection string must specify a database.");
    }

    var masterBuilder = new SqlConnectionStringBuilder(connectionString)
    {
        InitialCatalog = "master"
    };

    await using var masterConnection = new SqlConnection(masterBuilder.ConnectionString);
    await masterConnection.OpenAsync();

    // Create database if missing
    await using (var createDb = masterConnection.CreateCommand())
    {
        createDb.CommandText = "IF DB_ID(@db) IS NULL BEGIN DECLARE @sql nvarchar(max) = 'CREATE DATABASE ' + QUOTENAME(@db); EXEC(@sql); END";
        createDb.Parameters.Add(new SqlParameter("@db", builder.InitialCatalog));
        await createDb.ExecuteNonQueryAsync();
    }

    if (!builder.IntegratedSecurity &&
        !string.IsNullOrWhiteSpace(builder.UserID) &&
        !builder.UserID.Equals("sa", StringComparison.OrdinalIgnoreCase))
    {
        // Ensure login exists for SQL auth scenarios
        await using (var createLogin = masterConnection.CreateCommand())
        {
            createLogin.CommandText = """
IF NOT EXISTS (SELECT 1 FROM sys.sql_logins WHERE name = @login)
BEGIN
    DECLARE @sql nvarchar(max) = 'CREATE LOGIN ' + QUOTENAME(@login) + ' WITH PASSWORD = @pwd';
    EXEC sp_executesql @sql, N'@pwd nvarchar(256)', @pwd=@password;
END
""";
            createLogin.Parameters.Add(new SqlParameter("@login", builder.UserID));
            createLogin.Parameters.Add(new SqlParameter("@password", builder.Password));
            await createLogin.ExecuteNonQueryAsync();
        }

        // Ensure database user exists and can manage the schema
        await using var dbConnection = new SqlConnection(builder.ConnectionString);
        await dbConnection.OpenAsync();

        await using var createUser = dbConnection.CreateCommand();
        createUser.CommandText = """
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @user)
BEGIN
    DECLARE @sql nvarchar(max) = 'CREATE USER ' + QUOTENAME(@user) + ' FOR LOGIN ' + QUOTENAME(@login);
    EXEC(@sql);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members drm
    JOIN sys.database_principals p ON drm.member_principal_id = p.principal_id
    WHERE drm.role_principal_id = DATABASE_PRINCIPAL_ID('db_owner') AND p.name = @user)
BEGIN
    DECLARE @roleSql nvarchar(max) = 'ALTER ROLE db_owner ADD MEMBER ' + QUOTENAME(@user);
    EXEC(@roleSql);
END
""";
        createUser.Parameters.Add(new SqlParameter("@user", builder.UserID));
        createUser.Parameters.Add(new SqlParameter("@login", builder.UserID));
        await createUser.ExecuteNonQueryAsync();

        log.LogInformation("Ensured database {Database} and login/user {User} exist for {Environment}.", builder.InitialCatalog, builder.UserID, env.EnvironmentName);
    }
    else
    {
        log.LogInformation("Ensured database {Database} exists (integrated security or sysadmin login).", builder.InitialCatalog);
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

static void ConfigureTelemetry(IServiceCollection services, IConfiguration configuration)
{
    var connectionString = configuration["ApplicationInsights:ConnectionString"];
    var usePaidAzure = ShouldUsePaidAzure(configuration);

    if (usePaidAzure && !string.IsNullOrWhiteSpace(connectionString))
    {
        services.AddApplicationInsightsTelemetry(opt =>
        {
            opt.ConnectionString = connectionString;
            opt.EnableAdaptiveSampling = true;
        });
    }
    else
    {
        services.Configure<ApplicationInsightsServiceOptions>(opt => opt.EnableAdaptiveSampling = false);
    }
}

static bool ShouldUsePaidAzure(IConfiguration configuration)
{
    var tier = configuration["Deployment:Tier"] ?? configuration["Deployment__Tier"] ?? "Free";
    if (tier.Equals("Paid", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    return configuration.GetValue("Deployment:UsePaidFeatures", false);
}

static void RegisterSqlBackgroundJobs(IServiceCollection services)
{
    services.AddScoped<IBackgroundJobQueue, BackgroundJobQueue>();
    services.AddScoped<BackgroundJobProcessor>();
    services.AddHostedService<BackgroundJobHostedService>();
}

static void RegisterStorageQueueBackgroundJobs(IServiceCollection services)
{
    services.AddSingleton<StorageQueueBackgroundJobQueue>();
    services.AddSingleton<IBackgroundJobQueue>(sp => sp.GetRequiredService<StorageQueueBackgroundJobQueue>());
    services.AddHostedService<StorageQueueBackgroundJobProcessor>();
}

static bool TryValidateStorageQueue(IConfiguration configuration, out string? reason)
{
    var connection = configuration["BackgroundJobs:StorageQueue:ConnectionString"];
    if (string.IsNullOrWhiteSpace(connection))
    {
        reason = "BackgroundJobs:StorageQueue:ConnectionString missing";
        return false;
    }

    var queueName = configuration["BackgroundJobs:StorageQueue:QueueName"] ?? "tripshare-jobs";
    var poisonQueueName = configuration["BackgroundJobs:StorageQueue:PoisonQueueName"] ?? $"{queueName}-poison";

    try
    {
        var options = new QueueClientOptions
        {
            Retry =
            {
                MaxRetries = 1,
                Delay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(2)
            }
        };

        var queue = new QueueClient(connection, queueName, options);
        var poisonQueue = new QueueClient(connection, poisonQueueName, options);

        queue.Exists();
        poisonQueue.Exists();
        reason = null;
        return true;
    }
    catch (Exception ex) when (ex is RequestFailedException or HttpRequestException or SocketException or AggregateException)
    {
        reason = ex.Message;
        return false;
    }
}
