using ManagedBackgroundServices.Abstractions.Infrastructure;
using ManagedBackgroundServices.Abstractions.Logging;
using ManagedBackgroundServices.Abstractions.Queues;
using ManagedBackgroundServices.Abstractions.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ManagedBackgroundServices.Abstractions;

public static class ServiceExtensions
{
    private const int DefaultInMemoryLogCapacity = 250;

    public static IServiceCollection AddQueue<TQueue>(
        this IServiceCollection services,
        Action<QueueHandlersBuilder> configure)
        where TQueue : DurableQueue
        => AddQueue<TQueue>(
            services,
            configure,
            sp => ActivatorUtilities.CreateInstance<TQueue>(sp));

    public static IServiceCollection AddQueue<TQueue>(
        this IServiceCollection services,
        Action<QueueHandlersBuilder> configure,
        Func<IServiceProvider, TQueue> queueFactory)
        where TQueue : DurableQueue
    {
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(queueFactory);

        if (services.Any(sd =>
            sd.ServiceType == typeof(QueueBuilderRegistrationMarker) ||
            sd.ServiceType == typeof(DurableQueue)))
        {
            throw new InvalidOperationException("A durable queue has already been registered. Call AddQueue only once and register all handlers within that call.");
        }

        services.TryAddSingleton<QueueBuilderRegistrationMarker>();
        services.AddDurableQueue(queueFactory);
        services.TryAddSingleton<IQueueHandlerRegistry, QueueHandlerRegistry>();

        var builder = new QueueHandlersBuilder();
        configure(builder);

        var existingMessageTypes = services
            .Where(sd => sd.ServiceType == typeof(QueueHandlerRegistration))
            .Select(sd => sd.ImplementationInstance)
            .OfType<QueueHandlerRegistration>()
            .Select(x => x.MessageType)
            .ToHashSet();

        foreach (var entry in builder.Handlers)
        {
            var registration = entry.Registration;
            if (!existingMessageTypes.Add(registration.MessageType))
            {
                throw new InvalidOperationException($"A queue handler has already been registered for {registration.MessageType.Name}.");
            }

            services.AddSingleton(registration);
            entry.RegisterServices(services);
        }

        return services;
    }

    /// <summary>
    /// Registers a queue consumer for a specific message type, resolving the queue from DI.
    /// The DurableQueue must already be registered in the service collection, such as via AddDurableQueue or AddQueue.
    /// Can be called multiple times to register multiple queue consumers for different message types.
    /// </summary>
    /// <typeparam name="TMessage">The message type this consumer handles</typeparam>
    /// <typeparam name="TWorker">The IPayloadBackgroundWorker implementation</typeparam>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddQueueConsumer<TMessage, TWorker>(
        this IServiceCollection services)
        where TMessage : notnull
        where TWorker : class, IQueueWorker<TMessage>
    {
        AddManagedBackgroundServiceInfrastructure(services);
        RegisterQueueConsumer<TMessage, TWorker>(services, sp => sp.GetRequiredService<DurableQueue>());

        return services;
    }

    /// <summary>
    /// Registers a DurableQueue instance in the dependency injection container using a factory function.
    /// This enables lazy initialization and dependency resolution for your queue implementation.
    /// </summary>
    /// <typeparam name="TQueue">The DurableQueue implementation type</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="queueFactory">Factory function to create the queue instance</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDurableQueue<TQueue>(
        this IServiceCollection services,
        Func<IServiceProvider, TQueue> queueFactory)
        where TQueue : DurableQueue
    {
        ArgumentNullException.ThrowIfNull(queueFactory);

        services.AddSingleton<TQueue>(queueFactory);
        services.AddSingleton<DurableQueue>(sp => sp.GetRequiredService<TQueue>());
        AddManagedBackgroundServiceInfrastructure(services);

        return services;
    }

    private static void RegisterQueueConsumer<TMessage, TWorker>(
        IServiceCollection services,
        Func<IServiceProvider, DurableQueue> queueResolver)
        where TMessage : notnull
        where TWorker : class, IQueueWorker<TMessage>
    {
        services.AddSingleton<TWorker>();
        services.AddSingleton<QueueConsumerBackgroundService<TMessage>>(sp =>
            new QueueConsumerBackgroundService<TMessage>(
                sp.GetRequiredService<ILoggerFactory>(),
                queueResolver(sp),
                sp.GetRequiredService<TWorker>()));
        services.AddSingleton<ManagedBackgroundService>(sp => sp.GetRequiredService<QueueConsumerBackgroundService<TMessage>>());
        services.AddHostedService(sp => sp.GetRequiredService<QueueConsumerBackgroundService<TMessage>>());
    }

    /// <summary>
    /// Registers multiple scheduled jobs that run on recurrence patterns.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Configures scheduled jobs and their recurrence patterns</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddScheduledJobs(
        this IServiceCollection services,
        Action<ScheduledJobsBuilder> configure)
        => AddScheduledJobs(services, TimeProvider.System, configure);

    /// <summary>
    /// Registers multiple scheduled jobs that run on recurrence patterns with a custom TimeProvider.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="timeProvider">The time provider to use for scheduling</param>
    /// <param name="configure">Configures scheduled jobs and their recurrence patterns</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddScheduledJobs(
        this IServiceCollection services,
        TimeProvider timeProvider,
        Action<ScheduledJobsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(configure);

        AddManagedBackgroundServiceInfrastructure(services);
        services.TryAddSingleton<IScheduledJobRegistry, ScheduledJobRegistry>();

        var builder = new ScheduledJobsBuilder();
        configure(builder);

        var existingJobTypes = services
            .Where(sd => sd.ServiceType == typeof(ScheduledJobRegistration))
            .Select(sd => sd.ImplementationInstance)
            .OfType<ScheduledJobRegistration>()
            .Select(x => x.JobType)
            .ToHashSet();

        foreach (var registration in builder.Jobs)
        {
            if (!existingJobTypes.Add(registration.JobType))
            {
                throw new InvalidOperationException($"A scheduled job has already been registered for {registration.JobType.Name}.");
            }

            services.AddSingleton(registration);
            services.AddSingleton(registration.JobType);
        }

        // Register the TimeProvider as a singleton so it can be injected into ScheduledJobsExecutor
        services.TryAddSingleton(timeProvider);

        // Register a single ScheduledJobsExecutor to manage all jobs
        services.AddSingleton<ScheduledJobsExecutor>();
        services.AddSingleton<ManagedBackgroundService>(sp => sp.GetRequiredService<ScheduledJobsExecutor>());
        services.AddHostedService(sp => sp.GetRequiredService<ScheduledJobsExecutor>());

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

        // Only register the host once to avoid duplicates
        var hasHost = services.Any(x =>
            x.ServiceType == typeof(IHostedService) &&
            x.ImplementationType == typeof(ManagedBackgroundServicesHost));
        if (!hasHost)
        {
            services.AddHostedService<ManagedBackgroundServicesHost>();
        }

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

internal sealed class QueueBuilderRegistrationMarker;

/// <summary>
/// Hosted service that starts all registered ManagedBackgroundService instances.
/// </summary>
internal sealed class ManagedBackgroundServicesHost(IManagedBackgroundServiceProvider provider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tasks = provider.Services.Select(s => s.StartAsync(stoppingToken)).ToList();
        if (tasks.Any())
        {
            await Task.WhenAll(tasks);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        var tasks = provider.Services.Select(s => s.StopAsync(cancellationToken)).ToList();
        if (tasks.Any())
        {
            await Task.WhenAll(tasks);
        }
        await base.StopAsync(cancellationToken);
    }
}
