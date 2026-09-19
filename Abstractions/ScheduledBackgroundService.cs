using Microsoft.Extensions.Logging;

namespace ManagedBackgroundServices.Abstractions;

public interface INextRun
{
    public DateTimeOffset NextRunTime { get; }
}

public abstract class ScheduledBackgroundService(
    ILoggerFactory loggerFactory,
    TimeProvider timeProvider) : ManagedBackgroundService(loggerFactory), INextRun
{
    protected readonly TimeProvider TimeProvider = timeProvider;

    protected abstract Task<DateTimeOffset> GetNextRunTimeAsync(DateTimeOffset currentTime);

    protected abstract Task ExecuteScheduledAsync(CancellationToken stoppingToken);

    public DateTimeOffset NextRunTime { get; private set; }

    protected override async Task ExecuteInternalAsync(CancellationToken stoppingToken)
    {
        var now = TimeProvider.GetUtcNow();

        var nextRunTime = await GetNextRunTimeAsync(now);

        if (nextRunTime <= NextRunTime)
        {
            throw new InvalidOperationException($"{GetType().Name} returned a next run time that does not move forward. Current: {NextRunTime:O}, Next: {nextRunTime:O}");
        }

        NextRunTime = nextRunTime;

        if (now < NextRunTime)
        {
            await Task.Delay(NextRunTime - now, TimeProvider, stoppingToken);
            if (stoppingToken.IsCancellationRequested) return;
        }

        await ExecuteScheduledAsync(stoppingToken);
    }
}
