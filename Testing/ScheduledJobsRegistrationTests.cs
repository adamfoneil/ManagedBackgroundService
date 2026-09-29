using ManagedBackgroundServices.Abstractions;
using ManagedBackgroundServices.Abstractions.Infrastructure;
using ManagedBackgroundServices.Abstractions.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Testing;

[TestClass]
public sealed class ScheduledJobsRegistrationTests
{
    [TestMethod]
    public void AddScheduledJobs_RegistersManagedServicesAndRegistry()
    {
        var services = new ServiceCollection();

        services.AddScheduledJobs(schedule =>
        {
            schedule.Add<FirstScheduledJob>("*1hr");
            schedule.Add<SecondScheduledJob>("d[mon..fri] t[7:30am] tz:America/New_York");
        });

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IScheduledJobRegistry>();
        var managedServices = provider.GetRequiredService<IManagedBackgroundServiceProvider>().Services.ToList();

        Assert.HasCount(2, registry.Jobs);
        Assert.HasCount(1, managedServices);
        Assert.AreEqual("ScheduledJobsExecutor", managedServices[0].HandlerIdentifier);
    }

    [TestMethod]
    public void AddScheduledJobs_RejectsDuplicateJobTypes()
    {
        var services = new ServiceCollection();

        try
        {
            services.AddScheduledJobs(schedule =>
            {
                schedule.Add<FirstScheduledJob>("*1hr");
                schedule.Add<FirstScheduledJob>("*2hr");
            });
            Assert.Fail("Expected duplicate registration to throw.");
        }
        catch (InvalidOperationException ex)
        {
            Assert.Contains(nameof(FirstScheduledJob), ex.Message);
        }
    }

    [TestMethod]
    public void AddScheduledJobs_OnlyRegistersOneHost()
    {
        var services = new ServiceCollection();

        services.AddScheduledJobs(schedule => schedule.Add<FirstScheduledJob>("*1hr"));
        services.AddScheduledJobs(schedule => schedule.Add<SecondScheduledJob>("*2hr"));

        var hostRegistrations = services
            .Where(x => x.ServiceType == typeof(IHostedService) &&
                        x.ImplementationType?.Name == "ManagedBackgroundServicesHost")
            .ToList();

        Assert.HasCount(1, hostRegistrations);
    }

    private sealed class FirstScheduledJob(ILoggerFactory loggerFactory) : ManagedBackgroundService(loggerFactory)
    {
        protected override Task ExecuteInternalAsync(CancellationToken stoppingToken) => Task.CompletedTask;
    }

    private sealed class SecondScheduledJob(ILoggerFactory loggerFactory) : ManagedBackgroundService(loggerFactory)
    {
        protected override Task ExecuteInternalAsync(CancellationToken stoppingToken) => Task.CompletedTask;
    }
}
