using ManagedBackgroundServices.Abstractions.Infrastructure;

namespace WebDemo.BackgroundJobs;

public class FrequentJob(ILoggerFactory loggerFactory) : ManagedBackgroundService(loggerFactory)
{
    protected override Task ExecuteInternalAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("I'm doing frequent work");
        return Task.CompletedTask;
    }
}
