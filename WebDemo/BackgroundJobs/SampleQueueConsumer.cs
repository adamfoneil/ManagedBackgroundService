using ManagedBackgroundServices.Abstractions;

namespace WebDemo.BackgroundJobs;

public record SampleMessage(string Content);

public class SampleQueueConsumer(ILoggerFactory loggerFactory, DurableQueue persistentQueue) 
    : QueueConsumerBackgroundService(loggerFactory, persistentQueue)
{
    protected override Dictionary<string, QueueMessageHandler> MessageHandlers =>
        new()
        {
            [nameof(SampleMessage)] = HandleSampleMessage
        };

    async Task HandleSampleMessage(object message, CancellationToken cancellationToken)
    {
        if (message is SampleMessage sampleMessage)
        {
            Logger.LogInformation("Processing message: {message}", sampleMessage.Content);
            await Task.Delay(Random.Shared.Next(2, 7) * 1000, cancellationToken);
        }
    }
}
