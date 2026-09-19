namespace ManagedBackgroundServices.Abstractions.Infrastructure;

public interface IManagedBackgroundServiceProvider
{
    IReadOnlyCollection<ManagedBackgroundService> Services { get; }
}
