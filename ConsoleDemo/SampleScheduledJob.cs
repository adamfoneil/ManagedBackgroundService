using ManagedBackgroundServices.Abstractions.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ConsoleDemo;

public class DoWorkJob(ILogger<DoWorkJob> logger) : IBackgroundWorker
{
    private readonly ILogger<DoWorkJob> _logger = logger;
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DoWork at {now}", _timeProvider.GetLocalNow());
        await Task.CompletedTask;
    }
}

public class AnotherTaskJob(ILogger<AnotherTaskJob> logger) : IBackgroundWorker
{
    private readonly ILogger<AnotherTaskJob> _logger = logger;
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AnotherTask at {now}", _timeProvider.GetLocalNow());
        await Task.CompletedTask;
    }
}
