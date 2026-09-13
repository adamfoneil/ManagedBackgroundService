namespace ManagedBackgroundServices.Abstractions;

public interface IManagedBackgroundServiceProvider
{
    IReadOnlyCollection<ManagedBackgroundService> Services { get; }
}
