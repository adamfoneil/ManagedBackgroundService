using System.Text.Json;

namespace ManagedBackgroundServices.Abstractions.Queues;

public abstract class DurableQueue
{
    protected abstract Task StoreMessageAsync(Message message);

    protected abstract Task<IEnumerable<Message>> DequeueMessagesAsync(string handlerName, int batchSize, string machineName, CancellationToken stoppingToken);

    public record Message(
        DateTime Timestamp,
        string TypeName, // full type name needed for json deserialization
        string HandlerName, // needed for handler routing (short type name)
        string MachineName,        
        string JsonData,
        string? UserName = null);

    public async Task EnqueueAsync<T>(T payload, string? userName = null, JsonSerializerOptions? options = null)
    {
        var payloadJson = JsonSerializer.Serialize(payload, options);
        await StoreMessageAsync(new(DateTime.UtcNow, $"{typeof(T).FullName!}, {typeof(T).Assembly.GetName()}", typeof(T).Name, Environment.MachineName, payloadJson, userName));
    }

    public async Task<Message[]> DequeueAsync(string handlerName, int batchSize, CancellationToken stoppingToken)
    {
        var messages = await DequeueMessagesAsync(handlerName, batchSize, Environment.MachineName, stoppingToken);
        return [.. messages];
    }
}
