using ManagedBackgroundServices.Abstractions.Logging;
using ManagedBackgroundServices.Abstractions.Queues;
using ManagedBackgroundServices.Abstractions.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace ManagedBackgroundServices.Abstractions.Infrastructure;

public static class ServiceExtensions
{
    private const int DefaultInMemoryLogCapacity = 250;

    /// <summary>
    /// Registers a queue consumer for a specific message type.
    /// Can be called multiple times to register multiple queue consumers for different message types.
    /// </summary>
    /// <typeparam name="TMessage">The message type this consumer handles</typeparam>
    /// <typeparam name="TWorker">The IPayloadBackgroundWorker implementation</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="queue">The durable queue instance</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddQueueConsumer<TMessage, TWorker>(
        this IServiceCollection services, 
        DurableQueue queue)
        where TMessage : notnull
        where TWorker : class, IPayloadBackgroundWorker<TMessage>
    {
        ArgumentNullException.ThrowIfNull(queue);

        AddQueueInfrastructure(services, queue);
        services.AddSingleton<TWorker>();
        services.AddSingleton<QueueConsumerBackgroundService<TMessage>>(sp =>
            new QueueConsumerBackgroundService<TMessage>(
                sp.GetRequiredService<ILoggerFactory>(),
                queue,
                sp.GetRequiredService<TWorker>()));
        services.AddSingleton<ManagedBackgroundService>(sp => sp.GetRequiredService<QueueConsumerBackgroundService<TMessage>>());
        services.AddHostedService(sp => sp.GetRequiredService<QueueConsumerBackgroundService<TMessage>>());

        return services;
    }

    /// <summary>
    /// Registers a queue consumer for a specific message type with a DurableQueue factory.
    /// Can be called multiple times to register multiple queue consumers for different message types.
    /// </summary>
    /// <typeparam name="TMessage">The message type this consumer handles</typeparam>
    /// <typeparam name="TWorker">The IPayloadBackgroundWorker implementation</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="queueFactory">Factory function to create the durable queue</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddQueueConsumer<TMessage, TWorker>(
        this IServiceCollection services,
        Func<IServiceProvider, DurableQueue> queueFactory)
        where TMessage : notnull
        where TWorker : class, IPayloadBackgroundWorker<TMessage>
    {
        ArgumentNullException.ThrowIfNull(queueFactory);

        AddQueueInfrastructure(services, queueFactory);
        services.AddSingleton<TWorker>();
        services.AddSingleton<QueueConsumerBackgroundService<TMessage>>(sp =>
            new QueueConsumerBackgroundService<TMessage>(
                sp.GetRequiredService<ILoggerFactory>(),
                queueFactory(sp),
                sp.GetRequiredService<TWorker>()));
        services.AddSingleton<ManagedBackgroundService>(sp => sp.GetRequiredService<QueueConsumerBackgroundService<TMessage>>());
        services.AddHostedService(sp => sp.GetRequiredService<QueueConsumerBackgroundService<TMessage>>());

        return services;
    }

    private static void AddQueueInfrastructure(IServiceCollection services, DurableQueue queue)
    {
        services.AddSingleton(queue);
        AddManagedBackgroundServiceInfrastructure(services);
    }

    private static void AddQueueInfrastructure(IServiceCollection services, Func<IServiceProvider, DurableQueue> queueFactory)
    {
        services.AddSingleton(queueFactory);
        AddManagedBackgroundServiceInfrastructure(services);
    }

    /// <summary>
    /// Registers a scheduled job that runs on a recurrence pattern.
    /// Can be called multiple times to register multiple scheduled jobs with different patterns.
    /// </summary>
    /// <typeparam name="TWorker">The IBackgroundWorker implementation</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="patternString">The recurrence pattern string (parsed via RecurrencePattern.Parse)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddScheduledJob<TWorker>(
        this IServiceCollection services,
        string patternString)
        where TWorker : class, IBackgroundWorker
    {
        if (string.IsNullOrWhiteSpace(patternString))
            throw new ArgumentException("Pattern string cannot be null or empty.", nameof(patternString));

        var pattern = RecurrencePattern.Parse(patternString);

        AddManagedBackgroundServiceInfrastructure(services);
        services.AddSingleton<TWorker>();
        services.AddSingleton<ScheduledBackgroundService>(sp =>
            new ScheduledBackgroundService(
                sp.GetRequiredService<ILoggerFactory>(),
                TimeProvider.System,
                pattern,
                sp.GetRequiredService<TWorker>()));
        services.AddSingleton<ManagedBackgroundService>(sp => sp.GetRequiredService<ScheduledBackgroundService>());
        services.AddHostedService(sp => sp.GetRequiredService<ScheduledBackgroundService>());

        return services;
    }

    /// <summary>
    /// Registers a scheduled job that runs on a recurrence pattern with a custom TimeProvider.
    /// Can be called multiple times to register multiple scheduled jobs with different patterns.
    /// </summary>
    /// <typeparam name="TWorker">The IBackgroundWorker implementation</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="patternString">The recurrence pattern string (parsed via RecurrencePattern.Parse)</param>
    /// <param name="timeProvider">The time provider to use for scheduling</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddScheduledJob<TWorker>(
        this IServiceCollection services,
        string patternString,
        TimeProvider timeProvider)
        where TWorker : class, IBackgroundWorker
    {
        if (string.IsNullOrWhiteSpace(patternString))
            throw new ArgumentException("Pattern string cannot be null or empty.", nameof(patternString));
        if (timeProvider is null)
            throw new ArgumentNullException(nameof(timeProvider));

        var pattern = RecurrencePattern.Parse(patternString);

        AddManagedBackgroundServiceInfrastructure(services);
        services.AddSingleton<TWorker>();
        services.AddSingleton<ScheduledBackgroundService>(sp =>
            new ScheduledBackgroundService(
                sp.GetRequiredService<ILoggerFactory>(),
                timeProvider,
                pattern,
                sp.GetRequiredService<TWorker>()));
        services.AddSingleton<ManagedBackgroundService>(sp => sp.GetRequiredService<ScheduledBackgroundService>());
        services.AddHostedService(sp => sp.GetRequiredService<ScheduledBackgroundService>());

        return services;
    }

    public static void AddInMemoryLogger(this IServiceCollection services, int maxCapacity = DefaultInMemoryLogCapacity)
    {
        if (maxCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(maxCapacity), "Capacity must be greater than zero.");

        AddInMemoryLoggerInfrastructure(services, maxCapacity);
    }

    private static void AddManagedBackgroundServiceInfrastructure(IServiceCollection services)
    {
        services.TryAddSingleton<IManagedBackgroundServiceProvider, ManagedBackgroundServiceProvider>();
        AddInMemoryLoggerInfrastructure(services, DefaultInMemoryLogCapacity);
    }

    private static void AddInMemoryLoggerInfrastructure(IServiceCollection services, int maxCapacity)
    {
        var existingProviderDescriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(InMemoryLoggerProvider));
        if (existingProviderDescriptor is not null)
        {
            if (existingProviderDescriptor.ImplementationInstance is InMemoryLoggerProvider)
            {
                return;
            }

            throw new InvalidOperationException($"An {nameof(InMemoryLoggerProvider)} is already registered in an unsupported way. Register it via {nameof(AddInMemoryLogger)}.");
        }

        var provider = new InMemoryLoggerProvider(maxCapacity);
        services.AddLogging(builder => builder.AddProvider(provider));
        services.AddSingleton(provider);
        services.TryAddSingleton<IInMemoryLogQuery>(sp => sp.GetRequiredService<InMemoryLoggerProvider>());
    }
}
