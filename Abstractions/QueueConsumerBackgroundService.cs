using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ManagedBackgroundServices.Abstractions;

public interface IQueueConsumerPerformance
{
    decimal ConsumeRate { get; }
    TimeSpan ConsumeRateSpan { get; }
}

public delegate Task QueueMessageHandler(object message, CancellationToken stoppingToken);

public abstract class QueueConsumerBackgroundService(
    ILoggerFactory loggerFactory,
    DurableQueue persistentQueue) : ManagedBackgroundService(loggerFactory), IQueueConsumerPerformance
{
    private readonly DurableQueue _persistentQueue = persistentQueue;

    /// <summary>
    /// Returns a registry mapping TypeName to message handler instances.
    /// Key: TypeName (as stored in QueueMessage.TypeName)
    /// Value: IMessageHandler implementation for that type
    /// </summary>
    protected abstract Dictionary<string, QueueMessageHandler> MessageHandlers { get; }

    protected virtual async Task OnMessageFailedAsync(DurableQueue.QueueMessage queueMessage, object? messageObject, Exception exception)
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
            var messages = await _persistentQueue.DequeueAsync(DequeueBatchSize, stoppingToken);

            if (!messages.Any())
            {
                await Task.Delay(EmptyQueueDelay, stoppingToken);
                continue;
            }

            foreach (var queueMessage in messages)
            {
                object? msgObject = null;

                try
                {
                    if (!MessageHandlers.TryGetValue(queueMessage.HandlerName, out var handler))
                    {
                        Logger.LogWarning("No handler registered for message type {TypeName}", queueMessage.TypeName);
                        continue;
                    }

                    msgObject = DeserializeMessage(queueMessage.TypeName, queueMessage.JsonData);
                    if (msgObject is null)
                    {
                        Logger.LogWarning("Message json deserialized to null: {data}", queueMessage.JsonData);
                        continue;
                    }

                    await handler.Invoke(msgObject, stoppingToken);
                    _consumed++;                    
                }
                catch (Exception exc)
                {
                    Logger.LogError(exc, "Error processing message type {TypeName}", queueMessage.TypeName);
                    await OnMessageFailedAsync(queueMessage, msgObject, exc);
                }
            }

            await Task.Delay(ProcessingDelay, stoppingToken);
        }
    }

    /// <summary>
    /// Deserializes JsonData to the appropriate type based on TypeName.
    /// Override to customize type resolution logic (e.g., namespace handling).
    /// </summary>
    protected virtual object? DeserializeMessage(string typeName, string jsonData)
    {
        var messageType = Type.GetType(typeName);
        if (messageType == null)
        {
            Logger.LogWarning("Could not resolve type {TypeName}", typeName);
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(jsonData, messageType);
        }
        catch (Exception exc)
        {
            Logger.LogError(exc, "Failed to deserialize message of type {TypeName}", typeName);
            throw;
        }
    }
}
