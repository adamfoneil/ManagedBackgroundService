using ManagedBackgroundServices.Abstractions.Infrastructure;

namespace WebDemo.BackgroundJobs;

public class DbCleanupJob(ILoggerFactory loggerFactory) : ManagedBackgroundService(loggerFactory)
{
    protected override async Task ExecuteInternalAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("Running database cleanup job");
        await Task.Delay(100, stoppingToken);
    }
}

public class ReindexJob(ILoggerFactory loggerFactory) : ManagedBackgroundService(loggerFactory)
{
    protected override async Task ExecuteInternalAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("Running reindex job");
        await Task.Delay(150, stoppingToken);
    }
}

public class WeeklyReportsJob(ILoggerFactory loggerFactory) : ManagedBackgroundService(loggerFactory)
{
    protected override async Task ExecuteInternalAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("Running weekly reports job");
        await Task.Delay(200, stoppingToken);
    }
}
