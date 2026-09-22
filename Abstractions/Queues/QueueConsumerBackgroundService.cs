using System.Text.Json;
using ManagedBackgroundServices.Abstractions.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ManagedBackgroundServices.Abstractions.Queues;

public interface IQueueConsumerPerformance
{
    decimal ConsumeRate { get; }
    TimeSpan ConsumeRateSpan { get; }
    DateTime? LastConsumedDateTimeUtc { get; }
}

/// <summary>
/// Single-handler queue consumer background service.
/// Handles one message type using IPayloadBackgroundWorker&lt;T&gt;.
/// </summary>
/// <typeparam name="T">The message type this consumer handles</typeparam>
public class QueueConsumerBackgroundService<T>(
    ILoggerFactory loggerFactory,
    DurableQueue persistentQueue,
    IPayloadBackgroundWorker<T> handler) : ManagedBackgroundService(loggerFactory), IQueueConsumerPerformance
    where T : notnull
{
    private readonly DurableQueue _persistentQueue = persistentQueue;
    private readonly IPayloadBackgroundWorker<T> _handler = handler;

    /// <summary>
    /// Returns the handler class name for identification in dashboard and logging.
    /// </summary>
    public override string HandlerIdentifier => _handler.GetType().Name;

    /// <summary>
    /// Gets the last time a message was successfully consumed (UTC).
    /// </summary>
    public DateTime? LastConsumedDateTimeUtc { get; private set; }

    protected virtual async Task OnMessageFailedAsync(DurableQueue.Message queueMessage, T? messageObject, Exception exception)
    {
        // do nothing by default
        await Task.CompletedTask;
    }

    protected virtual TimeSpan EmptyQueueDelay => TimeSpan.FromSeconds(5);
    protected virtual TimeSpan ProcessingDelay => TimeSpan.Zero;
    protected virtual int DequeueBatchSize => 3;
    public virtual TimeSpan ConsumeRateSpan => TimeSpan.FromMinutes(5);

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
            var messages = await _persistentQueue.DequeueAsync(typeof(T).Name, DequeueBatchSize, stoppingToken);

            if (!messages.Any())
            {
                await Task.Delay(EmptyQueueDelay, stoppingToken);
                continue;
            }

            foreach (var queueMessage in messages)
            {
                T? msgObject = default;

                using (Logger.BeginScope(new Dictionary<string, object> { { "HandlerName", HandlerIdentifier } }))
                {
                    try
                    {
                        msgObject = DeserializeMessage(queueMessage.TypeName, queueMessage.JsonData);
                        if (msgObject is null)
                        {
                            Logger.LogWarning("Message json deserialized to null: {data}", queueMessage.JsonData);
                            continue;
                        }

                        Logger.LogDebug("Invoking handler with payload {payload}", queueMessage.JsonData);
                        await _handler.ExecuteAsync(msgObject, stoppingToken);
                        _consumed++;
                        LastConsumedDateTimeUtc = DateTime.UtcNow;
                    }
                    catch (Exception exc)
                    {
                        Logger.LogError(exc, "Error processing message type {TypeName}", queueMessage.TypeName);
                        await OnMessageFailedAsync(queueMessage, msgObject, exc);
                    }
                }
            }

            await Task.Delay(ProcessingDelay, stoppingToken);
        }
    }

    /// <summary>
    /// Deserializes JsonData to the appropriate type based on TypeName.
    /// Override to customize type resolution logic (e.g., namespace handling).
    /// </summary>
    protected virtual T? DeserializeMessage(string typeName, string jsonData)
    {
        var messageType = Type.GetType(typeName);
        if (messageType == null)
        {
            Logger.LogWarning("Could not resolve type {TypeName}", typeName);
            return default;
        }

        try
        {
            return (T?)JsonSerializer.Deserialize(jsonData, messageType);
        }
        catch (Exception exc)
        {
            Logger.LogError(exc, "Failed to deserialize message of type {TypeName}", typeName);
            throw;
        }
    }
}
