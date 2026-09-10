using ManagedBackgroundServices.Abstractions;
using Microsoft.Extensions.Logging;

namespace Samples;

public class SampleScheduledJob(
    ILoggerFactory loggerFactory,
    TimeProvider timeProvider) : ScheduledBackgroundService(loggerFactory)
{
    private readonly TimeProvider _timeProvider = timeProvider;

    private TimeOnly[] _runTimes = [ new(15, 0), new(9, 0) ];
    private DateTimeOffset _nextRun;

    protected override Task ExecuteScheduledAsync(CancellationToken stoppingToken)
    {
        throw new NotImplementedException();
    }

    protected override DateTimeOffset GetNextRunTime()
    {
        throw new NotImplementedException();
    }
}
