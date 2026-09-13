using ManagedBackgroundServices.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Samples;

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
                services.AddManagedBackgroundService(sp => new SampleScheduledJob(
                    sp.GetRequiredService<ILoggerFactory>(),
                    RecurrencePattern.Parse("*4sec"), TimeProvider.System));
            })
            .Build();

        host.Services.GetRequiredService<ILogger<Program>>()
            .LogInformation("Sample host starting");

        await host.RunAsync();
    }
}
