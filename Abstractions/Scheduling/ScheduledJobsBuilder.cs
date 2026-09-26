using ManagedBackgroundServices.Abstractions.Infrastructure;

namespace ManagedBackgroundServices.Abstractions.Scheduling;

public sealed record ScheduledJobRegistration(Type JobType, RecurrencePattern Pattern);

public interface IScheduledJobRegistry
{
    IReadOnlyCollection<ScheduledJobRegistration> Jobs { get; }
}

public sealed class ScheduledJobsBuilder
{
    private readonly Dictionary<Type, ScheduledJobRegistration> _jobs = [];

    internal IReadOnlyCollection<ScheduledJobRegistration> Jobs => _jobs.Values;

    public ScheduledJobsBuilder Add<TJob>(string patternString)
        where TJob : ManagedBackgroundService
    {
        if (string.IsNullOrWhiteSpace(patternString))
            throw new ArgumentException("Pattern string cannot be null or empty.", nameof(patternString));

        var jobType = typeof(TJob);
        var registration = new ScheduledJobRegistration(jobType, RecurrencePattern.Parse(patternString));
        if (!_jobs.TryAdd(jobType, registration))
        {
            throw new InvalidOperationException($"A scheduled job has already been registered for {jobType.Name}.");
        }

        return this;
    }
}

internal sealed class ScheduledJobRegistry(IEnumerable<ScheduledJobRegistration> jobs) : IScheduledJobRegistry
{
    public IReadOnlyCollection<ScheduledJobRegistration> Jobs { get; } = [.. jobs];
}
