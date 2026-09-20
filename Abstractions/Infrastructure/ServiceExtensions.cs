using ManagedBackgroundServices.Abstractions.Logging;
using ManagedBackgroundServices.Abstractions.Queues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace ManagedBackgroundServices.Abstractions.Infrastructure;

public static class ServiceExtensions
{
    private const int DefaultInMemoryLogCapacity = 250;

    /// <summary>
    /// ensures that background service is added so you can inject it where needed and also ensure it runs in the background
    /// </summary>
    public static void AddManagedBackgroundService<T>(this IServiceCollection services) where T : ManagedBackgroundService
    {
        AddManagedBackgroundServiceInfrastructure(services);
        services.AddSingleton<T>();
        services.AddSingleton<ManagedBackgroundService>(sp => sp.GetRequiredService<T>());
        services.AddHostedService(sp => sp.GetRequiredService<T>());
    }

    public static void AddManagedBackgroundService<T>(this IServiceCollection services, Func<IServiceProvider, T> factory) where T : ManagedBackgroundService
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

    public static void AddDurableQueue<T>(this IServiceCollection services, Func<IServiceProvider, T> factory) where T : DurableQueue
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (factory is null) throw new ArgumentNullException(nameof(factory));
        services.AddSingleton<T>(factory);
        services.AddSingleton<DurableQueue>(sp => sp.GetRequiredService<T>());
    }

    public static void AddDurableQueue<T>(this IServiceCollection services, T instance) where T : DurableQueue
    {
        services.AddSingleton<T>(instance);
        services.AddSingleton<DurableQueue>(instance);
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
