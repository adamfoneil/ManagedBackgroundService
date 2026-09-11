using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ManagedBackgroundServices.Abstractions;

public enum Status
{
    /// <summary>
    /// inner loop running normally
    /// </summary>
    Running = 1,
    /// <summary>
    /// an error happened in the inner loop, so job is disabled (but outer loop is still running, so in can be resumed)
    /// </summary>
    Paused = 2,
    /// <summary>
    /// an exception escaped to the outer loop, app must be restarted because inner loop no longer running
    /// </summary>
    Crashed = 3
}

public abstract class ManagedBackgroundService : BackgroundService
{
    protected ILogger Logger { get; }

    public ManagedBackgroundService(ILoggerFactory loggerFactory)
    {
        Logger = loggerFactory.CreateLogger(GetType().FullName!);
    }
    
    /// <summary>
    /// this is your custom logic for the background service
    /// </summary>
    protected abstract Task ExecuteInternalAsync(CancellationToken stoppingToken);    
    /// <summary>
    /// defined pause time between inner loop cycles -- gives the machine a bit of a "breather" between executions
    /// </summary>
    protected virtual TimeSpan RunningDelay { get => TimeSpan.FromSeconds(2); }
    /// <summary>
    /// when paused, how long do we wait until checking again if we're running?
    /// </summary>
    protected virtual TimeSpan PausedDelay { get => TimeSpan.FromSeconds(10); }

    public Status Status { get; private set; }
    public DateTime StatusDateTimeUtc { get; private set; }
    public Exception? Exception { get; private set; }
    
    /// <summary>
    /// causes a Disabled service to start its inner loop again (assuming it's not Stopped)
    /// </summary>
    public void Resume()
    {
        if (Status == Status.Crashed) throw new InvalidOperationException("Can't resume a crashed BackgroundService; restart the app instead.");

        Status = Status.Running;
        StatusDateTimeUtc = DateTime.UtcNow;
        Logger.LogInformation("Resumed background service {type}", GetType().Name);
    }

    public void Pause()
    {
        if (Status == Status.Crashed) throw new InvalidOperationException("Can't pause a crashed BackgroundService; restart the app instead.");

        Status = Status.Paused;
        StatusDateTimeUtc = DateTime.UtcNow;
        Logger.LogInformation("Paused background service {type}", GetType().Name);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            Status = Status.Running;
            StatusDateTimeUtc = DateTime.UtcNow;
            Logger.LogInformation("Started background service {type} started at {now} UTC", GetType().Name, StatusDateTimeUtc);

            while (!stoppingToken.IsCancellationRequested)
            {
                if (Status != Status.Running)
                {                    
                    // if not enabled, don't execute main job, and don't spend too many cycles waiting for repair
                    await Task.Delay(PausedDelay, stoppingToken);
                    continue;
                }

                try
                {                    
                    // this is where your main logic goes
                    Exception = null;
                    await ExecuteInternalAsync(stoppingToken);
                    await Task.Delay(RunningDelay, stoppingToken);                    
                }
                catch (Exception exc)
                {
                    // catching here enables you to keep the main loop running, so you can possibly fix whatever issue happened, without restarting app
                    Pause();
                    Exception = exc;
                    Logger.LogError(exc, "Error in inner loop, background service {type} paused at {now} UTC", GetType().Name, StatusDateTimeUtc);                    
                }                
            }
        }
        catch (Exception exc) when (exc is OperationCanceledException)
        {
            // this happens during restarts/shutdown of host app
            Pause();
            Logger.LogWarning("App is restarting or shutting down, background service {type} stopped at {now}", GetType().Name, StatusDateTimeUtc);
        }
        catch (Exception exc)
        {
            // Catching here prevents impacting the host, but app must be restarted (you can't resume from this state because we've exited the core while loop)
            Status = Status.Crashed;
            StatusDateTimeUtc = DateTime.UtcNow;
            Exception = exc;
            Logger.LogError(exc, "Error in outer loop, background service {type} crashed at {now}", GetType().Name, StatusDateTimeUtc);
        }
    }
}
