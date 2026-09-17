using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace ManagedBackgroundServices.Abstractions;

public static class ServiceExtensions
{
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

    public static void AddInMemoryLogger(this IServiceCollection services, int maxCapacity = 1000)
    {
        var provider = new InMemoryLoggerProvider(maxCapacity);
        services.AddLogging(builder => builder.AddProvider(provider));
        services.AddSingleton(provider); // Expose for querying
    }

    private static void AddManagedBackgroundServiceInfrastructure(IServiceCollection services)
    {
        services.TryAddSingleton<IManagedBackgroundServiceProvider, ManagedBackgroundServiceProvider>();
    }
}
