namespace ManagedBackgroundServices.Abstractions;

public interface IManagedBackgroundServiceAccessor
{
    IReadOnlyCollection<ManagedBackgroundService> Services { get; }
}
