using ManagedBackgroundServices.Abstractions.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConsoleDemo;

internal class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "HH:mm:ss ";
                });
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureServices(services =>
            {
                services.AddHealthChecks().AddCheck<BackgroundServicesHealthCheck>("Background Services");

                services
                    .AddScheduledJob<DoWorkJob>("*4sec")
                    .AddScheduledJob<AnotherTaskJob>("*6sec");
            })
            .Build();

        host.Services.GetRequiredService<ILogger<Program>>()
            .LogInformation("Sample host starting");

        await host.RunAsync();
    }
}
