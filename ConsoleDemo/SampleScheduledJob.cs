using ManagedBackgroundServices.Abstractions.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ConsoleDemo;

public class DoWorkJob : IBackgroundWorker
{
    private readonly ILogger<DoWorkJob> _logger;
    private readonly TimeProvider _timeProvider;

    public DoWorkJob(ILogger<DoWorkJob> logger)
    {
        _logger = logger;
        _timeProvider = TimeProvider.System;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Scheduled work at {now}", _timeProvider.GetLocalNow());
        await Task.CompletedTask;
    }
}

public class AnotherTaskJob : IBackgroundWorker
{
    private readonly ILogger<AnotherTaskJob> _logger;
    private readonly TimeProvider _timeProvider;

    public AnotherTaskJob(ILogger<AnotherTaskJob> logger)
    {
        _logger = logger;
        _timeProvider = TimeProvider.System;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("I'm doing another task as of {now}", _timeProvider.GetLocalNow());
        await Task.CompletedTask;
    }
}
