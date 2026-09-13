namespace ManagedBackgroundServices.Abstractions;

internal sealed class ManagedBackgroundServiceProvider(IEnumerable<ManagedBackgroundService> services) : IManagedBackgroundServiceProvider
{
    public IReadOnlyCollection<ManagedBackgroundService> Services { get; } = [.. services];
}
