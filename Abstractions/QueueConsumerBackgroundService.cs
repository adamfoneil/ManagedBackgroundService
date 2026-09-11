using Microsoft.Extensions.Logging;

namespace ManagedBackgroundServices.Abstractions;

public abstract class QueueConsumerBackgroundService<TMessage>(ILoggerFactory loggerFactory) : ManagedBackgroundService(loggerFactory)
{
    protected abstract Task<TMessage[]> TryDequeueAsync(int batchSize, CancellationToken stoppingToken);

    protected abstract Task ExecuteQueuedWorkAsync(TMessage message, CancellationToken stoppingToken);

    protected virtual async Task TrackPoisonMessageAsync(TMessage message, Exception exception)
    {
        Logger.LogError(exception, "Error in queue consumer {type} with {@message}", GetType().Name, message);
        await Task.CompletedTask;
    }

    protected virtual TimeSpan EmptyQueueDelay => TimeSpan.FromSeconds(5);
    protected virtual TimeSpan ProcessingDelay => TimeSpan.Zero;

    protected virtual int DequeueBatchSize { get => 3; }

    protected override async Task ExecuteInternalAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var messages = await TryDequeueAsync(DequeueBatchSize, stoppingToken);

            if (!messages.Any())
            {
                await Task.Delay(EmptyQueueDelay, stoppingToken);
                continue;
            }

            foreach (var msg in messages)
            {
                try
                {
                    await ExecuteQueuedWorkAsync(msg, stoppingToken);
                }
                catch (Exception exc)
                {
                    await TrackPoisonMessageAsync(msg, exc);                    
                }
            }
            
            await Task.Delay(ProcessingDelay, stoppingToken);
        }
    }
}
