using ManagedBackgroundServices.Abstractions.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ManagedBackgroundServices.Abstractions.Scheduling;

/// <summary>
/// Single-job scheduled background service.
/// Runs one job on a recurrence pattern using IBackgroundWorker.
/// </summary>
public class ScheduledBackgroundService(
    ILoggerFactory loggerFactory,
    TimeProvider timeProvider,
    RecurrencePattern pattern,
    IBackgroundWorker worker) : ManagedBackgroundService(loggerFactory)
{
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly RecurrencePattern _pattern = pattern;
    private readonly IBackgroundWorker _worker = worker;
    private DateTimeOffset _nextRunTime = DateTimeOffset.MinValue;

    /// <summary>
    /// Returns the worker class name for identification in dashboard and logging.
    /// </summary>
    public override string HandlerIdentifier => _worker.GetType().Name;

    /// <summary>
    /// Override to handle exceptions thrown by the worker.
    /// Note that error has already been logged, so no need to log again.
    /// </summary>
    protected virtual async Task OnHandlerFailedAsync(Exception exception)
    {
        // do nothing by default
        await Task.CompletedTask;
    }

    protected override async Task ExecuteInternalAsync(CancellationToken stoppingToken)
    {
        // Initialize next run time
        var now = _timeProvider.GetUtcNow();
        _nextRunTime = _pattern.GetNextOccurrence(now);

        while (!stoppingToken.IsCancellationRequested)
        {
            now = _timeProvider.GetUtcNow();

            if (_nextRunTime <= now)
            {
                using (Logger.BeginScope(new Dictionary<string, object> { { "HandlerName", HandlerIdentifier } }))
                {
                    try
                    {
                        Logger.LogDebug("Executing scheduled job");
                        await _worker.ExecuteAsync(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Error executing scheduled job");
                        await OnHandlerFailedAsync(ex);
                    }
                }

                _nextRunTime = _pattern.GetNextOccurrence(now);
            }
            else
            {
                var delay = _nextRunTime - now;
                try
                {
                    await Task.Delay(delay, _timeProvider, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when stopping
                }
            }
        }
    }
}
