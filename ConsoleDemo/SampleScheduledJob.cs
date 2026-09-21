using ManagedBackgroundServices.Abstractions.Scheduling;
using Microsoft.Extensions.Logging;

namespace ConsoleDemo;

public class SampleScheduledJob(ILoggerFactory loggerFactory) 
    : ScheduledBackgroundService(loggerFactory, TimeProvider.System)
{
    protected override void RegisterHandlers()
    {
        Registry.Add("One Task", DoWork, "*4sec");
        Registry.Add("Another Task", AnotherTask, "*6sec");
    }

    private async Task DoWork(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Scheduled work at {now}", TimeProvider.GetLocalNow());
        await Task.CompletedTask;
    }

    private async Task AnotherTask(CancellationToken cancellationToken)
    {
        Logger.LogInformation("I'm doing another task as of {now}", TimeProvider.GetLocalNow());
        await Task.CompletedTask;
    }
}
