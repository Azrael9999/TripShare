using System.Threading.Channels;

namespace TripShare.Api.Services;

public interface IBackgroundJobQueue
{
    void Enqueue(string name, Func<CancellationToken, Task> job);
    ValueTask<QueuedJob> DequeueAsync(CancellationToken ct);
}

public sealed record QueuedJob(string Name, Func<CancellationToken, Task> Job);

public sealed class BackgroundJobQueue : IBackgroundJobQueue
{
    private readonly Channel<QueuedJob> _channel = Channel.CreateUnbounded<QueuedJob>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });
    private readonly ILogger<BackgroundJobQueue> _logger;

    public BackgroundJobQueue(ILogger<BackgroundJobQueue> logger)
    {
        _logger = logger;
    }

    public void Enqueue(string name, Func<CancellationToken, Task> job)
    {
        if (!_channel.Writer.TryWrite(new QueuedJob(name, job)))
        {
            _logger.LogWarning("Failed to enqueue job {Job}", name);
        }
    }

    public ValueTask<QueuedJob> DequeueAsync(CancellationToken ct)
        => _channel.Reader.ReadAsync(ct);
}

public sealed class BackgroundJobHostedService : BackgroundService
{
    private readonly IBackgroundJobQueue _queue;
    private readonly ILogger<BackgroundJobHostedService> _logger;

    public BackgroundJobHostedService(IBackgroundJobQueue queue, ILogger<BackgroundJobHostedService> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background job worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await _queue.DequeueAsync(stoppingToken);
                _logger.LogDebug("Executing job {Job}", job.Name);
                await job.Job(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // graceful shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing background job");
            }
        }
    }
}
