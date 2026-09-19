using ManagedBackgroundServices.Abstractions;

namespace WebDemo.BackgroundJobs;

public class AnotherRecurringJob(ILoggerFactory loggerFactory) : ScheduledBackgroundService(loggerFactory, TimeProvider.System)
{
    protected override async Task ExecuteScheduledAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("AnotherRecurringJob is running");

        await Task.CompletedTask;
    }

    protected override async Task<DateTimeOffset> GetNextRunTimeAsync(DateTimeOffset currentTime) => await Task.FromResult(currentTime.Add(TimeSpan.FromSeconds(5)));    
}
