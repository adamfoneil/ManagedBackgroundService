using ManagedBackgroundServices.Abstractions.Infrastructure;

namespace WebDemo.BackgroundJobs;

public class FrequentJob(ILogger<FrequentJob> logger) : IBackgroundWorker
{
    private readonly ILogger<FrequentJob> _logger = logger;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("I'm doing frequent work");
        await Task.CompletedTask;
    }
}
