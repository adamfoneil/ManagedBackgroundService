using ManagedBackgroundServices.Abstractions.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ConsoleDemo;

public class DoWorkJob(ILoggerFactory loggerFactory) : ManagedBackgroundService(loggerFactory)
{
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    protected override Task ExecuteInternalAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("DoWork at {now}", _timeProvider.GetLocalNow());
        return Task.CompletedTask;
    }
}

public class AnotherTaskJob(ILoggerFactory loggerFactory) : ManagedBackgroundService(loggerFactory)
{
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    protected override Task ExecuteInternalAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("AnotherTask at {now}", _timeProvider.GetLocalNow());
        return Task.CompletedTask;
    }
}
