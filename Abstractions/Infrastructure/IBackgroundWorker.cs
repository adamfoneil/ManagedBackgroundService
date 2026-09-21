namespace ManagedBackgroundServices.Abstractions.Infrastructure;

public interface IBackgroundWorker
{
    public Task ExecuteAsync(CancellationToken cancellationToken);
}
