using ManagedBackgroundServices.Abstractions.Infrastructure;
using ManagedBackgroundServices.Abstractions.Logging;
using ManagedBackgroundServices.Abstractions.Queues;
using ManagedBackgroundServices.Abstractions.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace ManagedBackgroundServices.Abstractions;

public static class ServiceExtensions
{
    private const int DefaultInMemoryLogCapacity = 250;
    private static readonly MethodInfo RegisterQueueConsumerFromRegistryMethod = typeof(ServiceExtensions)
        .GetMethod(nameof(RegisterQueueConsumerFromRegistry), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Could not resolve {nameof(RegisterQueueConsumerFromRegistry)}.");

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

        foreach (var registration in builder.Handlers)
        {
            if (!existingMessageTypes.Add(registration.MessageType))
            {
                throw new InvalidOperationException($"A queue handler has already been registered for {registration.MessageType.Name}.");
            }

            services.AddSingleton(registration);
            RegisterQueueConsumerFromRegistryMethod
                .MakeGenericMethod(registration.MessageType, registration.HandlerType)
                .Invoke(null, [services]);
        }

        return services;
    }

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
        RegisterQueueConsumer<TMessage, TWorker>(services, _ => queue);

        return services;
    }

    /// <summary>
    /// Registers a queue consumer for a specific message type, resolving the queue from DI.
    /// The DurableQueue must be registered in the service collection via AddDurableQueue.
    /// Can be called multiple times to register multiple queue consumers for different message types.
    /// </summary>
    /// <typeparam name="TMessage">The message type this consumer handles</typeparam>
    /// <typeparam name="TWorker">The IPayloadBackgroundWorker implementation</typeparam>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddQueueConsumer<TMessage, TWorker>(
        this IServiceCollection services)
        where TMessage : notnull
        where TWorker : class, IPayloadBackgroundWorker<TMessage>
    {
        AddManagedBackgroundServiceInfrastructure(services);
        RegisterQueueConsumer<TMessage, TWorker>(services, sp => sp.GetRequiredService<DurableQueue>());

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
        RegisterQueueConsumer<TMessage, TWorker>(services, queueFactory);

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
    /// Registers a DurableQueue instance in the dependency injection container.
    /// This enables injection of the queue throughout your application and provides cleaner
    /// integration with other services.
    /// </summary>
    /// <typeparam name="TQueue">The DurableQueue implementation type</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="queue">The queue instance to register</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDurableQueue<TQueue>(
        this IServiceCollection services,
        TQueue queue)
        where TQueue : DurableQueue
    {
        ArgumentNullException.ThrowIfNull(queue);

        services.AddSingleton<DurableQueue>(queue);
        AddManagedBackgroundServiceInfrastructure(services);

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

        services.AddSingleton(queueFactory);
        services.AddSingleton<DurableQueue>(sp => sp.GetRequiredService<TQueue>());
        AddManagedBackgroundServiceInfrastructure(services);

        return services;
    }

    private static void RegisterQueueConsumerFromRegistry<TMessage, TWorker>(IServiceCollection services)
        where TMessage : notnull
        where TWorker : class, IPayloadBackgroundWorker<TMessage>
        => RegisterQueueConsumer<TMessage, TWorker>(services, sp => sp.GetRequiredService<DurableQueue>());

    private static void RegisterQueueConsumer<TMessage, TWorker>(
        IServiceCollection services,
        Func<IServiceProvider, DurableQueue> queueResolver)
        where TMessage : notnull
        where TWorker : class, IPayloadBackgroundWorker<TMessage>
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
            services.AddSingleton<ManagedBackgroundService>(sp =>
                new ScheduledBackgroundService(
                    sp.GetRequiredService<ILoggerFactory>(),
                    timeProvider,
                    registration.Pattern,
                    (ManagedBackgroundService)sp.GetRequiredService(registration.JobType)));
        }

        return services;
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
        services.AddSingleton<ManagedBackgroundService>(sp =>
            new ScheduledBackgroundService(
                sp.GetRequiredService<ILoggerFactory>(),
                TimeProvider.System,
                pattern,
                new BackgroundWorkerAdapter<TWorker>(
                    sp.GetRequiredService<ILoggerFactory>(),
                    sp.GetRequiredService<TWorker>())));

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
        services.AddSingleton<ManagedBackgroundService>(sp =>
            new ScheduledBackgroundService(
                sp.GetRequiredService<ILoggerFactory>(),
                timeProvider,
                pattern,
                new BackgroundWorkerAdapter<TWorker>(
                    sp.GetRequiredService<ILoggerFactory>(),
                    sp.GetRequiredService<TWorker>())));

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

internal sealed class BackgroundWorkerAdapter<TWorker>(
    ILoggerFactory loggerFactory,
    TWorker worker) : ManagedBackgroundService(loggerFactory)
    where TWorker : class, IBackgroundWorker
{
    private readonly TWorker _worker = worker;

    public override string HandlerIdentifier => _worker.GetType().Name;

    protected override Task ExecuteInternalAsync(CancellationToken stoppingToken) => _worker.ExecuteAsync(stoppingToken);
}

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
