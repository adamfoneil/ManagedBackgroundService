namespace ManagedBackgroundServices.Abstractions.Interfaces;

public interface IPersistentQueue<TData>
{
    Task EnqueueAsync(TData data);
    Task<TData?> TryDequeueAsync();
}
