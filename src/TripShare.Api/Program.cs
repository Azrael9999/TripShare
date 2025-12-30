using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using DbUp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Enrichers.CorrelationId;
using TripShare.Api.Middleware;
using TripShare.Api.Services;
using TripShare.Application.Abstractions;
using TripShare.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, lc) =>
{
    lc
        .Enrich.FromLogContext()
        .Enrich.WithCorrelationId()
        .WriteTo.Console()
        .WriteTo.File("Logs/tripshare-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
        .ReadFrom.Configuration(ctx.Configuration);
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddControllers().AddJsonOptions(opt =>
{
    opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        if (builder.Environment.IsDevelopment() || origins.Length == 0)
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

builder.Services.AddHealthChecks();

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
});

// EF Core
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default"));
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
builder.Services.AddScoped<ISmsSender, TextLkSmsSender>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TripService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<RatingService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<BlockService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<SiteSettingsService>();
builder.Services.AddHostedService<BookingHousekeepingService>();

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
app.MapHealthChecks("/health");

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
