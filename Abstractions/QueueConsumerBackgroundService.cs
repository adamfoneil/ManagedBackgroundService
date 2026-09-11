using Microsoft.Extensions.Logging;

namespace ManagedBackgroundServices.Abstractions;

public abstract class QueueConsumerBackgroundService<TMessage>(ILoggerFactory loggerFactory) : ManagedBackgroundService(loggerFactory)
{
    protected abstract Task<TMessage?> TryDequeueAsync(CancellationToken stoppingToken);

    protected abstract Task ExecuteQueuedWorkAsync(TMessage message, CancellationToken stoppingToken);

    protected virtual TimeSpan DequeuePause { get => TimeSpan.FromSeconds(5); }

    protected override async Task ExecuteInternalAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var message = await TryDequeueAsync(stoppingToken);
            if (message is null)
            {
                await Task.Delay(DequeuePause, stoppingToken);
                continue;
            }

            try
            {
                await ExecuteQueuedWorkAsync(message, stoppingToken);
            }
            catch (Exception exc)
            {
                // todo: track/dead-letter?
                Logger.LogError(exc, "Error in queue consumer {type}", GetType().Name);                
            }

            await Task.Delay(DequeuePause, stoppingToken);
        }
    }
}
