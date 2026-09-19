using Microsoft.Extensions.Logging;

namespace ManagedBackgroundServices.Abstractions;

public interface IQueueConsumerPerformance
{
    decimal ConsumeRate { get; }
    TimeSpan ConsumeRateSpan { get; }
}

public abstract class QueueConsumerBackgroundService<TMessage>(ILoggerFactory loggerFactory) : ManagedBackgroundService(loggerFactory), IQueueConsumerPerformance
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
    public virtual TimeSpan ConsumeRateSpan { get => TimeSpan.FromMinutes(5); }

    private int _consumed = 0;
    private DateTime _windowStart = DateTime.UtcNow;

    public decimal ConsumeRate
    {
        get
        {
            var elapsed = DateTime.UtcNow - _windowStart;

            if (elapsed >= ConsumeRateSpan)
            {
                _consumed = 0;
                _windowStart = DateTime.UtcNow;
                return 0;
            }

            return elapsed.TotalSeconds > 0
                ? _consumed / (decimal)elapsed.TotalSeconds
                : 0;
        }
    }

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
                    _consumed++;

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
