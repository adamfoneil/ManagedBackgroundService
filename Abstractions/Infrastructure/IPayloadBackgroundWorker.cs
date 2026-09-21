namespace ManagedBackgroundServices.Abstractions.Infrastructure;

public interface IPayloadBackgroundWorker<T>
{
    public Task ExecuteAsync(T payload, CancellationToken cancellationToken);
}
