using ManagedBackgroundServices.Abstractions.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ManagedBackgroundServices.Abstractions.Scheduling;

/// <summary>
/// Helper class that encapsulates the scheduling loop logic for scheduled jobs.
/// This allows job classes to focus on the actual work via ExecuteJobAsync, while the runner handles timing.
/// </summary>
public sealed class ScheduledJobsRunner(
    ILogger logger,
    TimeProvider timeProvider,
    RecurrencePattern pattern)
{
    private readonly ILogger _logger = logger;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly RecurrencePattern _pattern = pattern;
    private DateTimeOffset _nextRunTime = DateTimeOffset.MinValue;

    /// <summary>
    /// Gets the next scheduled run time in UTC.
    /// </summary>
    public DateTimeOffset NextRunTime => _nextRunTime;

    /// <summary>
    /// Runs the scheduling loop until cancellation is requested.
    /// Calls executeJobAsync at the scheduled times.
    /// </summary>
    /// <param name="executeJobAsync">The delegate to call when the job should run</param>
    /// <param name="stoppingToken">Cancellation token</param>
    public async Task RunAsync(
        Func<CancellationToken, Task> executeJobAsync,
        CancellationToken stoppingToken)
    {
        ArgumentNullException.ThrowIfNull(executeJobAsync);

        // Initialize next run time
        var now = _timeProvider.GetUtcNow();
        _nextRunTime = _pattern.GetNextOccurrence(now);

        while (!stoppingToken.IsCancellationRequested)
        {
            now = _timeProvider.GetUtcNow();

            if (_nextRunTime <= now)
            {
                try
                {
                    _logger.LogDebug("Executing scheduled job");
                    await executeJobAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing scheduled job");
                    throw;
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
