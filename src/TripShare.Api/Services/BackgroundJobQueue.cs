using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TripShare.Domain.Entities;
using TripShare.Infrastructure.Data;

namespace TripShare.Api.Services;

public interface IBackgroundJobQueue
{
    Task<Guid> EnqueueNotificationAsync(NotificationWork payload, DateTimeOffset? runAfter = null, int maxAttempts = 5, CancellationToken ct = default);
}

public sealed record NotificationWork(Guid UserId, NotificationType Type, string Title, string Body, Guid? TripId, Guid? BookingId);

internal sealed class BackgroundJobQueue : IBackgroundJobQueue
{
    private const string NotificationJobName = "notification";
    private readonly AppDbContext _db;
    private readonly ILogger<BackgroundJobQueue> _logger;

    public BackgroundJobQueue(AppDbContext db, ILogger<BackgroundJobQueue> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Guid> EnqueueNotificationAsync(NotificationWork payload, DateTimeOffset? runAfter = null, int maxAttempts = 5, CancellationToken ct = default)
    {
        var job = new BackgroundJob
        {
            Name = NotificationJobName,
            Payload = JsonSerializer.Serialize(payload),
            Status = BackgroundJobStatus.Pending,
            Attempts = 0,
            MaxAttempts = Math.Clamp(maxAttempts, 1, 10),
            RunAfter = runAfter ?? DateTimeOffset.UtcNow
        };

        _db.BackgroundJobs.Add(job);
        await _db.SaveChangesAsync(ct);
        _logger.LogDebug("Enqueued notification job {JobId} for user {UserId}", job.Id, payload.UserId);
        return job.Id;
    }
}

internal sealed class BackgroundJobHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundJobHostedService> _logger;

    public BackgroundJobHostedService(IServiceScopeFactory scopeFactory, ILogger<BackgroundJobHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Durable background job worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<BackgroundJobProcessor>();
                var processed = await processor.ProcessBatchAsync(stoppingToken);
                if (processed == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // graceful shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing background jobs");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}

internal sealed class BackgroundJobProcessor
{
    private const string NotificationJobName = "notification";
    private readonly AppDbContext _db;
    private readonly NotificationService _notifications;
    private readonly ILogger<BackgroundJobProcessor> _logger;

    public BackgroundJobProcessor(AppDbContext db, NotificationService notifications, ILogger<BackgroundJobProcessor> logger)
    {
        _db = db;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var jobs = await _db.BackgroundJobs
            .Where(x => x.Status == BackgroundJobStatus.Pending && x.RunAfter <= now)
            .OrderBy(x => x.RunAfter)
            .Take(10)
            .ToListAsync(ct);

        if (jobs.Count == 0) return 0;

        foreach (var job in jobs)
        {
            await ProcessJobAsync(job, ct);
        }

        return jobs.Count;
    }

    private async Task ProcessJobAsync(BackgroundJob job, CancellationToken ct)
    {
        job.Status = BackgroundJobStatus.Processing;
        job.Attempts += 1;
        job.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        try
        {
            if (job.Name == NotificationJobName)
            {
                var payload = JsonSerializer.Deserialize<NotificationWork>(job.Payload)
                    ?? throw new InvalidOperationException("Notification payload missing.");

                await _notifications.CreateAsync(payload.UserId, payload.Type, payload.Title, payload.Body, payload.TripId, payload.BookingId, ct);
            }
            else
            {
                throw new InvalidOperationException($"Unknown job type '{job.Name}'.");
            }

            job.Status = BackgroundJobStatus.Succeeded;
            job.LastError = null;
            job.RunAfter = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            job.LastError = ex.Message;
            var backoffSeconds = Math.Min(300, (int)Math.Pow(2, job.Attempts));
            job.RunAfter = DateTimeOffset.UtcNow.AddSeconds(backoffSeconds);
            job.Status = job.Attempts >= job.MaxAttempts ? BackgroundJobStatus.Failed : BackgroundJobStatus.Pending;
            _logger.LogWarning(ex, "Job {JobId} failed attempt {Attempt}/{MaxAttempts}", job.Id, job.Attempts, job.MaxAttempts);
        }
        finally
        {
            job.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }
}
