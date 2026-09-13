using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ManagedBackgroundServices.Abstractions;

public class BackgroundServicesHealthCheck(IManagedBackgroundServiceProvider backgroundServices) : IHealthCheck
{
    private readonly IManagedBackgroundServiceProvider _backgroundServices = backgroundServices;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var unhealthy = _backgroundServices.Services
            .Where(s => s.Status != Status.Running)
            .Select(s => new { s.GetType().Name, Status = s.Status.ToString() })
            .ToArray();

        if (!unhealthy.Any()) return Task.FromResult(HealthCheckResult.Healthy("All managed background services running"));

        return Task.FromResult(HealthCheckResult.Unhealthy(
           $"{unhealthy.Length} managed background service(s) are not running.",
           data: unhealthy.ToDictionary(item => item.Name, item => (object)item.Status)));
    }
}
