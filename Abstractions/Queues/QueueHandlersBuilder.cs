using ManagedBackgroundServices.Abstractions.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedBackgroundServices.Abstractions.Queues;

public sealed record QueueHandlerRegistration(Type MessageType, Type HandlerType);

public interface IQueueHandlerRegistry
{
    IReadOnlyCollection<QueueHandlerRegistration> Handlers { get; }
}

public sealed class QueueHandlersBuilder
{
    private readonly Dictionary<Type, QueueHandlerBuilderEntry> _handlers = [];

    internal IReadOnlyCollection<QueueHandlerBuilderEntry> Handlers => _handlers.Values;

    public QueueHandlersBuilder Add<TMessage, THandler>()
        where TMessage : notnull
        where THandler : class, IPayloadBackgroundWorker<TMessage>
    {
        var messageType = typeof(TMessage);
        var registration = new QueueHandlerRegistration(messageType, typeof(THandler));
        var entry = new QueueHandlerBuilderEntry(
            registration,
            services => services.AddQueueConsumer<TMessage, THandler>());
        if (!_handlers.TryAdd(messageType, entry))
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

internal sealed record QueueHandlerBuilderEntry(
    QueueHandlerRegistration Registration,
    Action<IServiceCollection> RegisterServices);
