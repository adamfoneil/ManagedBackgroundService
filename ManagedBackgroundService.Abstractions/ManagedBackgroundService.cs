using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ManagedBackgroundService.Abstractions;

public enum Status
{
    // inner loop running normally
    Enabled = 1,
    // an error happened in the inner loop, so job is disabled (but outer loop is still running, so in theory it could be fixed and resume)
    Disabled = 2,
    // an exception escapped the outer loop, meaning app must be restarted
    Stopped = 3
}

public abstract class ManagedBackgroundService(
    ILogger<ManagedBackgroundService> logger) : BackgroundService
{
    protected readonly ILogger<ManagedBackgroundService> Logger = logger;

    protected abstract Task ExecuteInternalAsync(CancellationToken stoppingToken);

    protected abstract Status Status { get; }
    protected abstract Task SetStatusAsync(Status status);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (Status != Status.Enabled)
                {
                    // if not enabled, don't execute main job, and don't spend too many cycles waiting for repair
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    continue;
                }

                try
                {                    
                    // this is where your main logic goes
                    await ExecuteInternalAsync(stoppingToken);
                }
                catch (Exception exc)
                {
                    // catching here enables you to keep the main loop running, so you can possibly fix whatever issue happened, without restarting app
                    await SetStatusAsync(Status.Disabled);
                    Logger.LogError(exc, "Error in inner loop, background service {type} disabled", GetType().Name);
                }                
            }
        }
        catch (Exception exc)
        {
            // you don't want to escape inner loop. If so, app must be restarted
            await SetStatusAsync(Status.Stopped);
            Logger.LogError(exc, "Error in outer loop, background service {type} stopped", GetType().Name);
        }
    }
}
