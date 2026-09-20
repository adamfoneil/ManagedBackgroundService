using ManagedBackgroundServices.Abstractions.Scheduling;
using Microsoft.Extensions.Logging;

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

    protected override async Task<DateTimeOffset> GetNextRunTimeAsync(DateTimeOffset currentTime) => await Task.FromResult(_pattern.GetNextOccurrence(currentTime));
}
