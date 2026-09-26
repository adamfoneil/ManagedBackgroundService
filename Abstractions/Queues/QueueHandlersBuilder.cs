using ManagedBackgroundServices.Abstractions.Infrastructure;

namespace ManagedBackgroundServices.Abstractions.Queues;

public sealed record QueueHandlerRegistration(Type MessageType, Type HandlerType);

public interface IQueueHandlerRegistry
{
    IReadOnlyCollection<QueueHandlerRegistration> Handlers { get; }
}

public sealed class QueueHandlersBuilder
{
    private readonly Dictionary<Type, QueueHandlerRegistration> _handlers = [];

    internal IReadOnlyCollection<QueueHandlerRegistration> Handlers => _handlers.Values;

    public QueueHandlersBuilder Add<TMessage, THandler>()
        where TMessage : notnull
        where THandler : class, IPayloadBackgroundWorker<TMessage>
    {
        var messageType = typeof(TMessage);
        var registration = new QueueHandlerRegistration(messageType, typeof(THandler));
        if (!_handlers.TryAdd(messageType, registration))
        {
            throw new InvalidOperationException($"A queue handler has already been registered for {messageType.Name}.");
        }

        return this;
    }
}

internal sealed class QueueHandlerRegistry(IEnumerable<QueueHandlerRegistration> handlers) : IQueueHandlerRegistry
{
    public IReadOnlyCollection<QueueHandlerRegistration> Handlers { get; } = [.. handlers];
}
