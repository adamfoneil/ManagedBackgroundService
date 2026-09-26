using ManagedBackgroundServices.Abstractions;
using ManagedBackgroundServices.Abstractions.Infrastructure;
using ManagedBackgroundServices.Abstractions.Queues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Testing;

[TestClass]
public sealed class QueueRegistrationTests
{
    [TestMethod]
    public void AddQueue_RegistersHandlersRegistryQueueAndManagedServices()
    {
        var services = new ServiceCollection();

        services.AddQueue<TestDurableQueue>(handlers =>
        {
            handlers.Add<FirstMessage, FirstHandler>();
            handlers.Add<SecondMessage, SecondHandler>();
        });

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IQueueHandlerRegistry>();
        var queue = provider.GetRequiredService<DurableQueue>();
        var managedServices = provider.GetRequiredService<IManagedBackgroundServiceProvider>().Services;

        Assert.AreEqual(typeof(TestDurableQueue), queue.GetType());
        Assert.AreEqual(2, registry.Handlers.Count);
        CollectionAssert.AreEquivalent(
            new[] { nameof(FirstMessage), nameof(SecondMessage) },
            registry.Handlers.Select(x => x.MessageType.Name).ToArray());
        CollectionAssert.AreEquivalent(
            new[] { nameof(FirstHandler), nameof(SecondHandler) },
            managedServices.Select(x => x.HandlerIdentifier).ToArray());
        Assert.IsTrue(managedServices.All(x => x.GetType().IsGenericType &&
                                              x.GetType().GetGenericTypeDefinition() == typeof(QueueConsumerBackgroundService<>)));
    }

    [TestMethod]
    public void AddQueue_UsesFactoryForQueueConstruction()
    {
        var services = new ServiceCollection();

        services.AddQueue<TestDurableQueue>(handlers =>
        {
            handlers.Add<FirstMessage, FirstHandler>();
        }, sp => new TestDurableQueue("factory-created"));

        using var provider = services.BuildServiceProvider();
        var queue = (TestDurableQueue)provider.GetRequiredService<DurableQueue>();

        Assert.AreEqual("factory-created", queue.Name);
        Assert.AreSame(queue, provider.GetRequiredService<TestDurableQueue>());
    }

    [TestMethod]
    public void AddQueue_RejectsDuplicateMessageTypes()
    {
        var services = new ServiceCollection();

        try
        {
            services.AddQueue<TestDurableQueue>(handlers =>
            {
                handlers.Add<FirstMessage, FirstHandler>();
                handlers.Add<FirstMessage, AlternateFirstHandler>();
            });
            Assert.Fail("Expected duplicate registration to throw.");
        }
        catch (InvalidOperationException ex)
        {
            StringAssert.Contains(ex.Message, nameof(FirstMessage));
        }
    }

    [TestMethod]
    public void AddQueue_OnlyRegistersOneHost()
    {
        var services = new ServiceCollection();

        services.AddQueue<TestDurableQueue>(handlers => handlers.Add<FirstMessage, FirstHandler>());
        services.AddQueueConsumer<SecondMessage, SecondHandler>();

        var hostRegistrations = services
            .Where(x => x.ServiceType == typeof(IHostedService) &&
                        x.ImplementationType?.Name == "ManagedBackgroundServicesHost")
            .ToList();

        Assert.AreEqual(1, hostRegistrations.Count);
    }

    [TestMethod]
    public void AddQueue_RejectsMultipleQueueRegistrations()
    {
        var services = new ServiceCollection();

        services.AddQueue<TestDurableQueue>(handlers => handlers.Add<FirstMessage, FirstHandler>());

        try
        {
            services.AddQueue<TestDurableQueue>(handlers => handlers.Add<SecondMessage, SecondHandler>());
            Assert.Fail("Expected duplicate queue registration to throw.");
        }
        catch (InvalidOperationException ex)
        {
            StringAssert.Contains(ex.Message, "AddQueue");
        }
    }

    private sealed record FirstMessage(string Value);
    private sealed record SecondMessage(string Value);

    private sealed class FirstHandler : IPayloadBackgroundWorker<FirstMessage>
    {
        public Task ExecuteAsync(FirstMessage payload, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class AlternateFirstHandler : IPayloadBackgroundWorker<FirstMessage>
    {
        public Task ExecuteAsync(FirstMessage payload, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class SecondHandler : IPayloadBackgroundWorker<SecondMessage>
    {
        public Task ExecuteAsync(SecondMessage payload, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TestDurableQueue(string name = "default") : DurableQueue
    {
        public string Name { get; } = name;

        protected override Task StoreMessageAsync(Message message) => Task.CompletedTask;

        protected override Task<IEnumerable<Message>> DequeueMessagesAsync(string handlerName, int batchSize, string machineName, CancellationToken stoppingToken)
            => Task.FromResult<IEnumerable<Message>>([]);
    }
}
