using ManagedBackgroundServices.Abstractions.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ManagedBackgroundServices.Abstractions.Scheduling;

public abstract class ScheduledBackgroundService(
    ILoggerFactory loggerFactory,
    TimeProvider timeProvider) : ManagedBackgroundService(loggerFactory)
{
    protected readonly TimeProvider TimeProvider = timeProvider;
    public readonly ScheduledJobHandlerRegistry Registry = new();

    /// <summary>
    /// Override to register scheduled job handlers using Registry.Add().
    /// Called during service initialization before execution begins.
    /// </summary>
    protected abstract void RegisterHandlers();    

    /// <summary>
    /// Override to handle exceptions thrown by handlers.
    /// </summary>
    protected virtual async Task OnHandlerFailedAsync(string handlerName, Exception exception)
    {
        // do nothing by default
        await Task.CompletedTask;
    }

    protected override async Task ExecuteInternalAsync(CancellationToken stoppingToken)
    {
        // Register handlers on first execution
        RegisterHandlers();

        // Initialize next run times for all handlers
        var now = TimeProvider.GetUtcNow();
        foreach (var handler in Registry.GetHandlers())
        {
            handler.NextRunTime = handler.Pattern.GetNextOccurrence(now);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            now = TimeProvider.GetUtcNow();

            // Find the next handler(s) that should run
            var nextHandler = Registry.GetNextHandlerToRun(now);

            if (nextHandler is not null)
            {
                // Execute the handler
                try
                {
                    await nextHandler.Handler(stoppingToken);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error executing scheduled handler '{HandlerName}'", nextHandler.Name);
                    await OnHandlerFailedAsync(nextHandler.Name, ex);
                }

                // Update next run time for this handler
                nextHandler.NextRunTime = nextHandler.Pattern.GetNextOccurrence(now);
            }
            else
            {
                // Get the next wake-up time
                var nextWakeUp = Registry.GetNextWakeUpTime(now);
                if (nextWakeUp is null)
                {
                    // No handlers registered, delay for a bit
                    await Task.Delay(TimeSpan.FromSeconds(5), TimeProvider, stoppingToken);
                }
                else if (nextWakeUp.Value > now)
                {
                    // Sleep until the next handler should run
                    var delay = nextWakeUp.Value - now;
                    try
                    {
                        await Task.Delay(delay, TimeProvider, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when stopping
                    }
                }
            }
        }
    }
}
