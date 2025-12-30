using System.Text.Json;
using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using TripShare.Application.Abstractions;
using TripShare.Domain.Entities;

namespace TripShare.Api.Services;

internal sealed class StorageQueueBackgroundJobQueue : IBackgroundJobQueue
{
    private readonly QueueClient _queue;
    private readonly QueueClient _poisonQueue;
    private readonly ILogger<StorageQueueBackgroundJobQueue> _logger;
    private readonly int _maxDequeueCount;

    public StorageQueueBackgroundJobQueue(IConfiguration cfg, ILogger<StorageQueueBackgroundJobQueue> logger)
    {
        _logger = logger;

        var connection = cfg["BackgroundJobs:StorageQueue:ConnectionString"]
            ?? throw new InvalidOperationException("BackgroundJobs:StorageQueue:ConnectionString missing");
        var queueName = cfg["BackgroundJobs:StorageQueue:QueueName"] ?? "tripshare-jobs";
        var poison = cfg["BackgroundJobs:StorageQueue:PoisonQueueName"] ?? $"{queueName}-poison";

        _maxDequeueCount = int.TryParse(cfg["BackgroundJobs:StorageQueue:MaxDequeueCount"], out var max) && max > 0
            ? max
            : 5;

        _queue = new QueueClient(connection, queueName);
        _poisonQueue = new QueueClient(connection, poison);

        _queue.CreateIfNotExists();
        _poisonQueue.CreateIfNotExists();
    }

    public async Task<Guid> EnqueueNotificationAsync(NotificationWork payload, DateTimeOffset? runAfter = null, int maxAttempts = 5, CancellationToken ct = default)
    {
        var envelope = new StorageQueueEnvelope
        {
            Kind = "notification",
            Payload = JsonSerializer.Serialize(payload),
            NotBeforeUtc = runAfter ?? DateTimeOffset.UtcNow,
            MaxAttempts = Math.Clamp(maxAttempts, 1, 10)
        };

        var message = JsonSerializer.Serialize(envelope);
        var delay = runAfter.HasValue ? runAfter.Value - DateTimeOffset.UtcNow : TimeSpan.Zero;
        if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

        await _queue.SendMessageAsync(message, visibilityTimeout: delay, cancellationToken: ct);
        _logger.LogDebug("Enqueued notification job for user {UserId}", payload.UserId);
        return Guid.NewGuid();
    }

    internal sealed record StorageQueueEnvelope
    {
        public string Kind { get; init; } = "notification";
        public string Payload { get; init; } = string.Empty;
        public DateTimeOffset NotBeforeUtc { get; init; }
        public int MaxAttempts { get; init; } = 5;
    }

    internal QueueClient Queue => _queue;
    internal QueueClient PoisonQueue => _poisonQueue;
    internal int MaxDequeueCount => _maxDequeueCount;
}

internal sealed class StorageQueueBackgroundJobProcessor : BackgroundService
{
    private readonly StorageQueueBackgroundJobQueue _queueProvider;
    private readonly NotificationService _notifications;
    private readonly ILogger<StorageQueueBackgroundJobProcessor> _logger;

    public StorageQueueBackgroundJobProcessor(StorageQueueBackgroundJobQueue queueProvider, NotificationService notifications, ILogger<StorageQueueBackgroundJobProcessor> logger)
    {
        _queueProvider = queueProvider;
        _notifications = notifications;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Storage queue background job processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var messages = await _queueProvider.Queue.ReceiveMessagesAsync(16, TimeSpan.FromSeconds(30), stoppingToken);
                if (messages.Value.Length == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                foreach (var msg in messages.Value)
                {
                    await ProcessMessageAsync(msg, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // graceful shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing storage queue jobs");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ProcessMessageAsync(QueueMessage message, CancellationToken ct)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<StorageQueueBackgroundJobQueue.StorageQueueEnvelope>(message.MessageText);
            if (envelope is null)
            {
                await _queueProvider.Queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, ct);
                return;
            }

            if (envelope.NotBeforeUtc > DateTimeOffset.UtcNow)
            {
                // defer
                await _queueProvider.Queue.UpdateMessageAsync(message.MessageId, message.PopReceipt, message.MessageText, visibilityTimeout: envelope.NotBeforeUtc - DateTimeOffset.UtcNow, cancellationToken: ct);
                return;
            }

            if (envelope.Kind == "notification")
            {
                var payload = JsonSerializer.Deserialize<NotificationWork>(envelope.Payload);
                if (payload is null) throw new InvalidOperationException("Missing payload");

                await _notifications.CreateAsync(payload.UserId, payload.Type, payload.Title, payload.Body, payload.TripId, payload.BookingId, ct);
            }

            await _queueProvider.Queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Job message failed attempt {Attempt}", message.DequeueCount);

            if (message.DequeueCount >= _queueProvider.MaxDequeueCount)
            {
                try
                {
                    await _queueProvider.PoisonQueue.SendMessageAsync(message.MessageText, cancellationToken: ct);
                    await _queueProvider.Queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, ct);
                }
                catch (Exception poisonEx)
                {
                    _logger.LogError(poisonEx, "Failed to move message to poison queue");
                }
            }
            else
            {
                await _queueProvider.Queue.UpdateMessageAsync(message.MessageId, message.PopReceipt, message.MessageText, visibilityTimeout: TimeSpan.FromSeconds(5 * Math.Pow(2, message.DequeueCount)), cancellationToken: ct);
            }
        }
    }
}
