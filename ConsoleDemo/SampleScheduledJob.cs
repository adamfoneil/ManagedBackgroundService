using ManagedBackgroundServices.Abstractions.Scheduling;
using Microsoft.Extensions.Logging;

namespace Samples;

public class SampleScheduledJob(ILoggerFactory loggerFactory) 
    : ScheduledBackgroundService(loggerFactory, TimeProvider.System)
{
    protected override void RegisterHandlers()
    {
        Registry.Add("Sample Task", DoWork, "*4sec");
    }

    private async Task DoWork(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Scheduled work at {now}", TimeProvider.GetLocalNow());
        await Task.CompletedTask;
    }
}
