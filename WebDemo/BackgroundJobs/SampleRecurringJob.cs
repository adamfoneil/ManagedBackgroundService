using ManagedBackgroundServices.Abstractions;

namespace WebDemo.BackgroundJobs;

public class SampleRecurringJob(ILoggerFactory loggerFactory) : ScheduledBackgroundService(loggerFactory, TimeProvider.System)
{
    protected override async Task ExecuteScheduledAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("Scheduled job is running");
        await Task.CompletedTask;
    }

    protected override DateTimeOffset GetNextRunTime(DateTimeOffset currentTime) => currentTime.Add(TimeSpan.FromSeconds(3));
}
 
