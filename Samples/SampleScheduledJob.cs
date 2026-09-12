using ManagedBackgroundServices.Abstractions;
using Microsoft.Extensions.Logging;
using ScheduleAbstractions;

namespace Samples;

public class SampleScheduledJob(
    ILoggerFactory loggerFactory,
    RecurrencePattern pattern,
    TimeProvider timeProvider) : ScheduledBackgroundService(loggerFactory, timeProvider)
{    
    private readonly RecurrencePattern _pattern = pattern;

    protected override TimeSpan RunningDelay => TimeSpan.Zero;

    protected override Task ExecuteScheduledAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("Scheduled work at {now}, next at {next}", TimeProvider.GetLocalNow(), NextRunTime);        
        return Task.CompletedTask;
    }

    protected override DateTimeOffset GetNextRunTime(DateTimeOffset currentTime) => _pattern.GetNextOccurrence(currentTime);   
}
