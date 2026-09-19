using ManagedBackgroundServices.Abstractions;
using ManagedBackgroundServices.Abstractions.Infrastructure;

namespace WebDemo.BackgroundJobs;

public class SampleRecurringJob(ILoggerFactory loggerFactory) : ScheduledBackgroundService(loggerFactory, TimeProvider.System)
{    
    protected override async Task ExecuteScheduledAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("Scheduled job is running");

        /*
        // 25% chance to throw a simulated exception
        if (Random.Shared.NextDouble() < 0.15)
        {
            Logger.LogWarning("Scheduled job is about to fail (15% chance).");
            throw new InvalidOperationException("Simulated random failure (25% probability).");
        }
        */
        await Task.CompletedTask;
    }

    protected override async Task<DateTimeOffset> GetNextRunTimeAsync(DateTimeOffset currentTime) => await Task.FromResult(currentTime.Add(TimeSpan.FromSeconds(3)));
}
