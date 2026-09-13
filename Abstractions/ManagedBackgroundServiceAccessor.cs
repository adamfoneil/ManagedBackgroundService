namespace ManagedBackgroundServices.Abstractions;

internal sealed class ManagedBackgroundServiceAccessor(IEnumerable<ManagedBackgroundService> services) : IManagedBackgroundServiceAccessor
{
    public IReadOnlyCollection<ManagedBackgroundService> Services { get; } = services.ToArray();
}
