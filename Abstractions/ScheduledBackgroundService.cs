using Microsoft.Extensions.Logging;

namespace ManagedBackgroundServices.Abstractions;

public abstract class ScheduledBackgroundService(
    ILoggerFactory loggerFactory,
    TimeProvider timeProvider) : ManagedBackgroundService(loggerFactory)
{
    protected readonly TimeProvider TimeProvider = timeProvider;

    protected abstract DateTimeOffset GetNextRunTime(DateTimeOffset currentTime);

    protected abstract Task ExecuteScheduledAsync(CancellationToken stoppingToken);

    public DateTimeOffset NextRunTime { get; private set; }

    protected override async Task ExecuteInternalAsync(CancellationToken stoppingToken)
    {
        var now = TimeProvider.GetUtcNow();

        if (NextRunTime == default)
        {
            NextRunTime = GetNextRunTime(now);
        }

        if (now < NextRunTime)
        {
            var delay = NextRunTime - now;
            await Task.Delay(delay, TimeProvider, stoppingToken);
        }

        await ExecuteScheduledAsync(stoppingToken);

        var nextRunTime = GetNextRunTime(NextRunTime);

        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("Next run time for {type} is {time}", GetType().Name, nextRunTime);
        }

        if (nextRunTime <= NextRunTime)
        {
            throw new InvalidOperationException($"{GetType().Name} returned a next run time that does not move forward. Current: {NextRunTime:O}, Next: {nextRunTime:O}");
        }

        NextRunTime = nextRunTime;
    }
}
