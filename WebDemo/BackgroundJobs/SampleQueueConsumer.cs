using ManagedBackgroundServices.Abstractions;
using ManagedBackgroundServices.Abstractions.Infrastructure;

namespace WebDemo.BackgroundJobs;

public class SampleQueueConsumer(ILoggerFactory loggerFactory) : QueueConsumerBackgroundService<string>(loggerFactory), ICurrentWork
{
    public string CurrentWorkInfo { get; private set; } = string.Empty;

    protected override async Task ExecuteQueuedWorkAsync(string message, CancellationToken stoppingToken)
    {
        Logger.LogInformation("Consuming message: {message}", message);

        CurrentWorkInfo = $"Processing message: {message}";

        await Task.Delay(Random.Shared.Next(2, 7) * 1000, stoppingToken);
    }

    protected override async Task<string[]> TryDequeueAsync(int batchSize, CancellationToken stoppingToken)
    {        
        return ["hello", "goodbye", "whatever"];
    }
}
