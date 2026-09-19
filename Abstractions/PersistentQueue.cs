using System.Text.Json;

namespace ManagedBackgroundServices.Abstractions;

public abstract class PersistentQueue
{
    protected abstract Task StoreMessageAsync(QueueMessage message);

    protected abstract Task<IEnumerable<QueueMessage>> DequeueMessagesAsync(int batchSize, string machineName, CancellationToken stoppingToken);

    public record QueueMessage(
        DateTime Timestamp,
        string TypeName,
        string MachineName,
        string JsonData);

    public async Task EnqueueAsync<T>(T payload)
    {
        var payloadJson = JsonSerializer.Serialize(payload);
        await StoreMessageAsync(new(DateTime.UtcNow, typeof(T).Name, Environment.MachineName, payloadJson));
    }

    public abstract Task<QueueMessage[]> DequeueAsync(int batchSize, CancellationToken stoppingToken);
}
