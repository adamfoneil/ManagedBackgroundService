using ManagedBackgroundServices.Abstractions.Infrastructure;

namespace WebDemo.BackgroundJobs;

public record SampleMessage(string Content);

public class SampleMessageHandler(ILogger<SampleMessageHandler> logger) : IPayloadBackgroundWorker<SampleMessage>
{
    private readonly ILogger<SampleMessageHandler> _logger = logger;

    public async Task ExecuteAsync(SampleMessage payload, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing message: {message}", payload.Content);
        await Task.Delay(Random.Shared.Next(2, 7) * 1000, cancellationToken);
    }
}
