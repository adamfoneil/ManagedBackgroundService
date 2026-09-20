using ManagedBackgroundServices.Abstractions.Queues;

namespace WebDemo.BackgroundJobs;

public record SampleMessage(string Content);

public class SampleQueueConsumer(ILoggerFactory loggerFactory, DurableQueue persistentQueue) 
    : QueueConsumerBackgroundService(loggerFactory, persistentQueue)
{
    protected override void RegisterHandlers()
    {
        Registry.With<SampleMessage>(nameof(SampleMessage), HandleSampleMessage);
    }

    async Task HandleSampleMessage(DurableQueue.Message rawMessage, SampleMessage message, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Processing from {user} message: {message}", rawMessage.UserName, message.Content);
        await Task.Delay(Random.Shared.Next(2, 7) * 1000, cancellationToken);
    }
}
