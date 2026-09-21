using ManagedBackgroundServices.Abstractions.Scheduling;

namespace WebDemo.BackgroundJobs;

public class MyScheduledJobs(ILoggerFactory loggerFactory)
    : ScheduledBackgroundService(loggerFactory, TimeProvider.System)
{
    protected override void RegisterHandlers()
    {
        Registry.Add("Database cleanup", DbCleanup, "*1d t[2:00am]");
        Registry.Add("Reindex", Reindex, "d[mon..fri] t[9:30am, 3:30pm]");
        Registry.Add("End of Week Reports", WeeklyReports, "d[sat]");
    }

    public async Task DbCleanup(CancellationToken cancellationToken)
    {
        // Simulate database cleanup
        await Task.Delay(100, cancellationToken);
    }

    public async Task Reindex(CancellationToken cancellationToken)
    {
        // Simulate reindexing
        await Task.Delay(150, cancellationToken);
    }

    public async Task WeeklyReports(CancellationToken cancellationToken)
    {
        // Simulate weekly reports generation
        await Task.Delay(200, cancellationToken);
    }

    protected override async Task OnHandlerFailedAsync(string handlerName, Exception exception)
    {
        Logger.LogWarning("Handler '{HandlerName}' failed: {Message}", handlerName, exception.Message);
        await Task.CompletedTask;
    }
}
