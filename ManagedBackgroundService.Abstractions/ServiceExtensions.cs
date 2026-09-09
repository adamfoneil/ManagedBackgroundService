using Microsoft.Extensions.DependencyInjection;

namespace ManagedBackgroundService.Abstractions;

public static class ServiceExtensions
{
    public static void AddManagedBackgroundService<T>(this IServiceCollection services) where T : ManagedBackgroundService
    {
        services.AddSingleton<T>();
        services.AddHostedService(sp => sp.GetRequiredService<T>());
    }
}
