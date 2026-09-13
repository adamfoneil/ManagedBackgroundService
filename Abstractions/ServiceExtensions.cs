using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

    private static void AddManagedBackgroundServiceInfrastructure(IServiceCollection services)
    {
        services.TryAddSingleton<IManagedBackgroundServiceAccessor, ManagedBackgroundServiceAccessor>();
    }
}
