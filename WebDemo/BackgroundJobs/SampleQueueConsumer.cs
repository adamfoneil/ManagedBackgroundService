using ManagedBackgroundServices.Abstractions;

namespace WebDemo.BackgroundJobs;

public class SampleQueueConsumer(ILoggerFactory loggerFactory) : QueueConsumerBackgroundService<string>(loggerFactory)
{
    protected override async Task ExecuteQueuedWorkAsync(string message, CancellationToken stoppingToken)
    {
        Logger.LogInformation("Consuming message: {message}", message);

        await Task.Delay(Random.Shared.Next(2, 7) * 1000, stoppingToken);
    }

    protected override async Task<string[]> TryDequeueAsync(int batchSize, CancellationToken stoppingToken)
    {        
        return ["hello", "goodbye", "whatever"];
    }
}
