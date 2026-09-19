namespace ManagedBackgroundServices.Abstractions.Infrastructure;

internal sealed class ManagedBackgroundServiceProvider(IEnumerable<ManagedBackgroundService> services) : IManagedBackgroundServiceProvider
{
    public IReadOnlyCollection<ManagedBackgroundService> Services { get; } = [.. services];
}
