using System.Text.Json;
using ManagedBackgroundServices.Abstractions;
using ManagedBackgroundServices.Abstractions.Infrastructure;

namespace WebDemo.BackgroundJobs;

public record SampleMessage(string Content);

public class SampleQueueConsumer(ILoggerFactory loggerFactory, PersistentQueue persistentQueue) 
    : QueueConsumerBackgroundService(loggerFactory, persistentQueue), IStatusMessage
{
    public string StatusMessage { get; private set; } = string.Empty;

    protected override IReadOnlyDictionary<string, QueueMessageHandler> MessageHandlers =>
        new Dictionary<string, QueueMessageHandler>
        {
            [nameof(SampleMessage)] = HandleSampleMessage
        };

    protected override object? DeserializeMessage(string typeName, string jsonData)
    {
        var messageType = Type.GetType(typeName) 
            ?? Type.GetType($"{typeName}, WebDemo");

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

    async Task HandleSampleMessage(object message, CancellationToken cancellationToken)
    {
        if (message is SampleMessage sampleMessage)
        {
            Logger.LogInformation("Processing message: {message}", sampleMessage.Content);
            StatusMessage = $"Processing message: {sampleMessage.Content}";
            await Task.Delay(Random.Shared.Next(2, 7) * 1000, cancellationToken);
        }
    }
}
