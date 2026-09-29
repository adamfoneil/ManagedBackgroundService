using ManagedBackgroundServices.Abstractions.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ManagedBackgroundServices.Abstractions.Scheduling;

/// <summary>
/// Unified execution service that runs all scheduled jobs on their defined recurrence patterns.
/// This replaces the per-job wrapper pattern with a single service managing multiple jobs.
/// </summary>
internal sealed class ScheduledJobsExecutor(
    ILoggerFactory loggerFactory,
    IScheduledJobRegistry jobRegistry,
    IServiceProvider serviceProvider,
    TimeProvider timeProvider) : ManagedBackgroundService(loggerFactory)
{
    private readonly IScheduledJobRegistry _jobRegistry = jobRegistry;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly Dictionary<Type, ScheduledJobsRunner> _runners = [];
    private readonly Dictionary<Type, ManagedBackgroundService> _jobs = [];

    public override string HandlerIdentifier => "ScheduledJobsExecutor";

    /// <summary>
    /// Gets the next scheduled run times for all registered jobs.
    /// </summary>
    public IReadOnlyDictionary<string, DateTimeOffset> NextRunTimes
    {
        get
        {
            var result = new Dictionary<string, DateTimeOffset>();
            foreach (var (jobType, runner) in _runners)
            {
                result[jobType.Name] = runner.NextRunTime;
            }
            return result;
        }
    }

    protected override async Task ExecuteInternalAsync(CancellationToken stoppingToken)
    {
        // Initialize runners for all registered jobs
        foreach (var registration in _jobRegistry.Jobs)
        {
            var job = (ManagedBackgroundService)_serviceProvider.GetRequiredService(registration.JobType);
            var runner = new ScheduledJobsRunner(
                Logger,
                _timeProvider,
                registration.Pattern);

            _runners[registration.JobType] = runner;
            _jobs[registration.JobType] = job;
        }

        if (!_runners.Any())
        {
            Logger.LogWarning("No scheduled jobs registered");
            return;
        }

        // Run all jobs concurrently
        var tasks = _runners.Select(kvp =>
            RunJobAsync(kvp.Key, kvp.Value, _jobs[kvp.Key], stoppingToken)
        ).ToList();

        await Task.WhenAll(tasks);
    }

    private async Task RunJobAsync(
        Type jobType,
        ScheduledJobsRunner runner,
        ManagedBackgroundService job,
        CancellationToken stoppingToken)
    {
        try
        {
            await runner.RunAsync(
                async ct => await job.ExecuteOnceAsync(ct),
                stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in scheduled job runner for {jobType}", jobType.Name);
            throw;
        }
    }
}
