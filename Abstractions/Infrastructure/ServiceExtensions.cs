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
    /// Registers a queue consumer background service with a DurableQueue instance.
    /// The service will run in the background and can be injected where needed.
    /// </summary>
    public static void AddQueueConsumer<T>(this IServiceCollection services, DurableQueue queue) where T : QueueConsumerBackgroundService
    {
        if (queue is null) throw new ArgumentNullException(nameof(queue));
        AddQueueInfrastructure(services, queue);
        services.AddSingleton<T>();
        services.AddSingleton<ManagedBackgroundService>(sp => sp.GetRequiredService<T>());
        services.AddHostedService(sp => sp.GetRequiredService<T>());
    }

    /// <summary>
    /// Registers a queue consumer background service with a DurableQueue factory.
    /// The service will run in the background and can be injected where needed.
    /// </summary>
    public static void AddQueueConsumer<T>(this IServiceCollection services, Func<IServiceProvider, DurableQueue> queueFactory) where T : QueueConsumerBackgroundService
    {
        if (queueFactory is null) throw new ArgumentNullException(nameof(queueFactory));
        AddQueueInfrastructure(services, queueFactory);
        services.AddSingleton<T>();
        services.AddSingleton<ManagedBackgroundService>(sp => sp.GetRequiredService<T>());
        services.AddHostedService(sp => sp.GetRequiredService<T>());
    }

    /// <summary>
    /// Registers a queue consumer background service with a custom factory and a DurableQueue instance.
    /// The service will run in the background and can be injected where needed.
    /// </summary>
    public static void AddQueueConsumer<T>(this IServiceCollection services, DurableQueue queue, Func<IServiceProvider, T> consumerFactory) where T : QueueConsumerBackgroundService
    {
        if (queue is null) throw new ArgumentNullException(nameof(queue));
        if (consumerFactory is null) throw new ArgumentNullException(nameof(consumerFactory));
        AddQueueInfrastructure(services, queue);
        services.AddSingleton(consumerFactory);
        services.AddSingleton<ManagedBackgroundService>(sp => sp.GetRequiredService<T>());
        services.AddHostedService(sp => sp.GetRequiredService<T>());
    }

    /// <summary>
    /// Registers a queue consumer background service with both a DurableQueue factory and a consumer factory.
    /// The service will run in the background and can be injected where needed.
    /// </summary>
    public static void AddQueueConsumer<T>(this IServiceCollection services, Func<IServiceProvider, DurableQueue> queueFactory, Func<IServiceProvider, T> consumerFactory) where T : QueueConsumerBackgroundService
    {
        if (queueFactory is null) throw new ArgumentNullException(nameof(queueFactory));
        if (consumerFactory is null) throw new ArgumentNullException(nameof(consumerFactory));
        AddQueueInfrastructure(services, queueFactory);
        services.AddSingleton(consumerFactory);
        services.AddSingleton<ManagedBackgroundService>(sp => sp.GetRequiredService<T>());
        services.AddHostedService(sp => sp.GetRequiredService<T>());
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
    /// Registers a scheduled job handler that executes jobs based on recurrence patterns.
    /// The service will run in the background and can be injected where needed.
    /// </summary>
    public static void AddScheduledJobs<T>(this IServiceCollection services) where T : ScheduledBackgroundService
    {
        AddManagedBackgroundServiceInfrastructure(services);
        services.AddSingleton<T>();
        services.AddSingleton<ManagedBackgroundService>(sp => sp.GetRequiredService<T>());
        services.AddHostedService(sp => sp.GetRequiredService<T>());
    }

    /// <summary>
    /// Registers a scheduled job handler with a custom factory.
    /// The service will run in the background and can be injected where needed.
    /// </summary>
    public static void AddScheduledJobs<T>(this IServiceCollection services, Func<IServiceProvider, T> factory) where T : ScheduledBackgroundService
    {
        AddManagedBackgroundServiceInfrastructure(services);
        services.AddSingleton(factory);
        services.AddSingleton<ManagedBackgroundService>(sp => sp.GetRequiredService<T>());
        services.AddHostedService(sp => sp.GetRequiredService<T>());
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
