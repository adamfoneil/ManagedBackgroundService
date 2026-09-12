using Microsoft.Extensions.DependencyInjection;

namespace ManagedBackgroundServices.Abstractions;

public static class ServiceExtensions
{
    /// <summary>
    /// ensures that background service is added so you can inject it where needed and also ensure it runs in the background
    /// </summary>
    public static void AddManagedBackgroundService<T>(this IServiceCollection services) where T : ManagedBackgroundService
    {
        services.AddSingleton<T>();
        services.AddHostedService(sp => sp.GetRequiredService<T>());
    }

    public static void AddManagedBackgroundService<T>(this IServiceCollection services, Func<IServiceProvider, T> factory) where T : ManagedBackgroundService
    {
        services.AddSingleton(factory);
        services.AddHostedService(sp => sp.GetRequiredService<T>());
    }
}
