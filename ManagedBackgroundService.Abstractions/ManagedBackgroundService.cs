using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ManagedBackgroundService.Abstractions;

public enum Status
{
    /// <summary>
    /// inner loop running normally
    /// </summary>
    Enabled = 1,
    /// <summary>
    /// an error happened in the inner loop, so job is disabled (but outer loop is still running, so in theory it could be fixed and resume)
    /// </summary>
    Disabled = 2,
    /// <summary>
    /// an exception escaped to the outer loop, app must be restarted
    /// </summary>
    Stopped = 3
}

public abstract class ManagedBackgroundService : BackgroundService
{
    protected ILogger Logger { get; }

    protected ManagedBackgroundService(ILoggerFactory loggerFactory)
    {
        Logger = loggerFactory.CreateLogger(GetType().FullName!);
    }
    
    /// <summary>
    /// this is your custom logic for the background service
    /// </summary>
    protected abstract Task ExecuteInternalAsync(CancellationToken stoppingToken);    
    /// <summary>
    /// defined pause time between inner loop cycles
    /// </summary>
    protected abstract TimeSpan EnabledDelay { get; }
    protected virtual TimeSpan DisabledDelay { get => TimeSpan.FromMinutes(1); }

    public Status Status { get; private set; }
    public DateTime StatusDateTimeUtc { get; private set; }
    public Exception? Exception { get; private set; }
    /// <summary>
    /// causes a Disabled service to start its inner loop again (assuming it's not Stopped)
    /// </summary>
    public void Resume()
    {
        if (Status == Status.Stopped) throw new Exception("Can't resume a stopped BackgroundService; restart the app instead.");

        Status = Status.Enabled;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            Status = Status.Enabled;
            StatusDateTimeUtc = DateTime.UtcNow;

            while (!stoppingToken.IsCancellationRequested)
            {
                if (Status != Status.Enabled)
                {
                    // if not enabled, don't execute main job, and don't spend too many cycles waiting for repair
                    await Task.Delay(DisabledDelay, stoppingToken);
                    continue;
                }

                try
                {
                    // this is where your main logic goes
                    Exception = null;
                    await ExecuteInternalAsync(stoppingToken);
                    await Task.Delay(EnabledDelay, stoppingToken);                    
                }
                catch (Exception exc)
                {
                    // catching here enables you to keep the main loop running, so you can possibly fix whatever issue happened, without restarting app
                    Status = Status.Disabled;
                    StatusDateTimeUtc = DateTime.UtcNow;                    
                    Exception = exc;
                    Logger.LogError(exc, "Error in inner loop, background service {type} disabled", GetType().Name);
                }                
            }
        }
        catch (Exception exc) when (exc is OperationCanceledException)
        {
            // this can happen during normal restarts
            Status = Status.Disabled;
            StatusDateTimeUtc = DateTime.UtcNow;
            Logger.LogWarning("App is restarting, {type} job should resume", GetType().Name);
        }
        catch (Exception exc)
        {
            // Catching here prevents impacting the host, but app must be restarted to resume job            
            Status = Status.Stopped;
            StatusDateTimeUtc = DateTime.Now;
            Exception = exc;
            Logger.LogError(exc, "Error in outer loop, background service {type} stopped", GetType().Name);
        }
    }
}
