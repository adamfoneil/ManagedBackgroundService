using Microsoft.Extensions.Logging;

namespace ManagedBackgroundServices.Abstractions;

public abstract class QueueConsumerBackgroundService<TMessage>(ILoggerFactory loggerFactory) : ManagedBackgroundService(loggerFactory)
{
    protected abstract Task<TMessage[]> TryDequeueAsync(int batchSize, CancellationToken stoppingToken);

    protected abstract Task ExecuteQueuedWorkAsync(TMessage message, CancellationToken stoppingToken);

    protected virtual async Task OnMessageFailedAsync(TMessage message, Exception exception)
    {
        // do nothing by default
        await Task.CompletedTask;
    }

    protected virtual async Task OnMessageCompletedAsync(TMessage message)
    {
        // do nothing by default
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

                    try
                    {
                        await OnMessageCompletedAsync(msg);
                    }
                    catch (Exception exc)
                    {
                        Logger.LogError(exc, "Error in OnMessageCompletedAsync with {@message}", msg);
                    }
                }
                catch (Exception exc)
                {
                    Logger.LogError(exc, "Error in queue consumer {type} with {@message}", GetType().Name, msg);
                    await OnMessageFailedAsync(msg, exc);
                }
            }

            await Task.Delay(ProcessingDelay, stoppingToken);
        }
    }
}
