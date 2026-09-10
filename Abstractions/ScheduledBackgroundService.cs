using Microsoft.Extensions.Logging;

namespace ManagedBackgroundServices.Abstractions;

public abstract class ScheduledBackgroundService(
    ILoggerFactory loggerFactory,
    TimeProvider timeProvider) : ManagedBackgroundService(loggerFactory)
{
    private readonly TimeProvider _timeProvider = timeProvider;

    protected abstract DateTimeOffset GetNextRunTime(DateTimeOffset currentTime);

    protected abstract Task ExecuteScheduledAsync(CancellationToken stoppingToken);

    public DateTimeOffset NextRunTime { get; private set; }

    protected override async Task ExecuteInternalAsync(CancellationToken stoppingToken)
    {
        var now = _timeProvider.GetUtcNow();

        if (NextRunTime == default)
        {
            NextRunTime = GetNextRunTime(now);
        }

        if (now < NextRunTime)
        {
            return;
        }

        await ExecuteScheduledAsync(stoppingToken);

        NextRunTime = GetNextRunTime(NextRunTime);
    }    
}
