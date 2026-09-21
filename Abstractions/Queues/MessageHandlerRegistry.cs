namespace ManagedBackgroundServices.Abstractions.Queues;

/// <summary>
/// Registry for typed message handlers.
/// Stores handlers with their message types and provides type-safe invocation.
/// </summary>
public class MessageHandlerRegistry
{
    private readonly Dictionary<string, (Type MessageType, Delegate Handler)> _handlers = [];

    /// <summary>
    /// Registers a typed message handler for a specific message type.
    /// </summary>
    /// <typeparam name="T">The message type this handler processes</typeparam>
    /// <param name="handlerName">The handler name (typically the message type name)</param>
    /// <param name="handler">The typed handler delegate</param>
    public void Register<T>(string handlerName, QueueMessageHandler<T> handler) where T : notnull
    {
        _handlers[handlerName] = (typeof(T), handler);
    }

    /// <summary>
    /// Registers a typed message handler and returns this registry for fluent chaining.
    /// </summary>
    /// <typeparam name="T">The message type this handler processes</typeparam>
    /// <param name="handlerName">The handler name (typically the message type name)</param>
    /// <param name="handler">The typed handler delegate</param>
    /// <returns>This registry instance for fluent chaining</returns>
    public MessageHandlerRegistry Add<T>(string handlerName, QueueMessageHandler<T> handler) where T : notnull
    {
        Register(handlerName, handler);
        return this;
    }

    /// <summary>
    /// Attempts to get a handler for the specified handler name and message.
    /// </summary>
    /// <param name="handlerName">The handler name</param>
    /// <param name="message">The deserialized message object</param>
    /// <param name="handler">The matching handler delegate, or null if not found</param>
    /// <returns>True if a handler was found and the message type matches; false otherwise</returns>
    public bool TryGetHandler(string handlerName, object message, out Delegate? handler)
    {
        handler = null;

        if (!_handlers.TryGetValue(handlerName, out var entry))
        {
            return false;
        }

        if (message.GetType() != entry.MessageType)
        {
            return false;
        }

        handler = entry.Handler;
        return true;
    }
}
