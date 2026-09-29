namespace ManagedBackgroundServices.Abstractions.Infrastructure;

public interface IQueueWorker<T>
{
    public Task ExecuteAsync(T message, CancellationToken cancellationToken);
}
