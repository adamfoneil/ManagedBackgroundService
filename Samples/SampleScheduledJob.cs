using ManagedBackgroundServices.Abstractions;
using Microsoft.Extensions.Logging;

namespace Samples;

public class SampleScheduledJob(
    ILoggerFactory loggerFactory,
    TimeProvider timeProvider) : ScheduledBackgroundService(loggerFactory, timeProvider)
{
    private static readonly TimeOnly[] RunTimes = [new(9, 0), new(15, 0)];

    protected override Task ExecuteScheduledAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine($"Doing scheduled work at {TimeProvider.GetLocalNow():O}. Next scheduled run was {NextRunTime:O}.");
        return Task.CompletedTask;
    }

    protected override DateTimeOffset GetNextRunTime(DateTimeOffset currentTime)
    {
        var timeZone = TimeProvider.LocalTimeZone;
        var localNow = TimeZoneInfo.ConvertTime(currentTime, timeZone);
        var localDate = DateOnly.FromDateTime(localNow.DateTime);

        foreach (var runTime in RunTimes)
        {
            var candidate = CreateScheduledInstant(localDate, runTime, timeZone);

            if (candidate > currentTime)
            {
                return candidate;
            }
        }

        return CreateScheduledInstant(localDate.AddDays(1), RunTimes[0], timeZone);
    }

    private static DateTimeOffset CreateScheduledInstant(DateOnly date, TimeOnly time, TimeZoneInfo timeZone)
    {
        var localDateTime = date.ToDateTime(time, DateTimeKind.Unspecified);

        if (timeZone.IsInvalidTime(localDateTime))
        {
            localDateTime = localDateTime.AddHours(1);
        }

        if (timeZone.IsAmbiguousTime(localDateTime))
        {
            var offsets = timeZone.GetAmbiguousTimeOffsets(localDateTime);
            var chosenOffset = offsets.Max();
            return new DateTimeOffset(localDateTime, chosenOffset).ToUniversalTime();
        }

        var offset = timeZone.GetUtcOffset(localDateTime);
        return new DateTimeOffset(localDateTime, offset).ToUniversalTime();
    }
}
