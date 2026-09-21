using ManagedBackgroundServices.Abstractions.Infrastructure;

namespace WebDemo.BackgroundJobs;

public class DbCleanupJob(ILogger<DbCleanupJob> logger) : IBackgroundWorker
{
    private readonly ILogger<DbCleanupJob> _logger = logger;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running database cleanup job");
        await Task.Delay(100, cancellationToken);
    }
}

public class ReindexJob(ILogger<ReindexJob> logger) : IBackgroundWorker
{
    private readonly ILogger<ReindexJob> _logger = logger;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running reindex job");
        await Task.Delay(150, cancellationToken);
    }
}

public class WeeklyReportsJob(ILogger<WeeklyReportsJob> logger) : IBackgroundWorker
{
    private readonly ILogger<WeeklyReportsJob> _logger = logger;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running weekly reports job");
        await Task.Delay(200, cancellationToken);
    }
}
