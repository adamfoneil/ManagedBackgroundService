using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ManagedBackgroundServices.Abstractions;

public class BackgroundServicesHealthCheck(IManagedBackgroundServiceProvider backgroundServices) : IHealthCheck
{
    private readonly IManagedBackgroundServiceProvider _backgroundServices = backgroundServices;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var unhealthy = _backgroundServices.Services
            .Where(s => s.Status == Status.Crashed)
            .Select(s => new { s.GetType().Name, Status = s.Status.ToString() })
            .ToArray();

        if (unhealthy.Any())
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"{unhealthy.Length} managed background service(s) have crashed.",
                data: unhealthy.ToDictionary(item => item.Name, item => (object)item.Status)));
        }

        var degraded = _backgroundServices.Services
            .Where(s => s.Status == Status.Paused)
            .Select(s => new { s.GetType().Name, Status = s.Status.ToString() })
            .ToArray();

        if (degraded.Any())
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"{degraded.Length} managed background service(s) are paused.",
                data: degraded.ToDictionary(item => item.Name, item => (object)item.Status)));
        }

        return Task.FromResult(HealthCheckResult.Healthy("All managed background services running"));
    }
}
