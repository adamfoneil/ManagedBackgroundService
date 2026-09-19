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
