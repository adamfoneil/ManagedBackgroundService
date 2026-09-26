There are a wealth background job frameworks and libraries out there -- Hangfire, Quartz, and Coravel come to mind. ASP.NET Core has its own [BackgroundService](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0&tabs=visual-studio#backgroundservice-base-class). As a matter of preference, I happen to like Coravel. But more than that, I like the flexibility that the native BackgroundService offers. The problem with BackgroundService is its very flexibility. It's a bare-minimum service shell that I have found too easy to mis-use. This project, therefore, is a thin wrapper around the native BackgroundService, adding a few features to give you more of a "pit of success."

First off, what makes BackgroundService hard to use? When you create an empty BackgroundService and create its default implementation, you start with some boilerplate:

```csharp
internal class SampleBackgroundService : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        throw new NotImplementedException();
    }
}
```

The problem with this is it's not clear what pattern to implement to give you a service that can be stopped and started at will, or for that matter scheduled -- that can handle errors and be recoverable at runtime. There's a core `while` loop you need along with some carefully positioned `try/catch` blocks along with a little bit of state management. It's not obvious at all from the default boilerplate what this should look like.

That's where [ManagedBackgroundService](Abstractions/ManagedBackgroundService.cs) comes in. It's an abstract class just like the native BackgroundService. The work your service does goes in the `ExecuteInternalAsync` method. There are `Pause` and `Resume` methods that do what they sound like, along with a `Status` property that returns Running, Paused, or Crashed. If your `ExecuteInternalAsync` throws an exception, the service goes into a Paused state. A paused job can be resumed. If an exception occurs outside the inner `try` block, the job goes into a Crashed state. If that happens, it can't be resumed, and you must restart the host app.

This library has two derived classes that are the focus of this library:
- [QueueConsumerBackgroundService](Abstractions/Queues/QueueConsumerBackgroundService.cs)
- [ScheduledBackgroundService](Abstractions/Scheduling/ScheduledBackgroundService.cs)

# Using QueueConsumerBackgroundService

[QueueConsumerBackgroundService](Abstractions/Queues/QueueConsumerBackgroundService.cs) is for processing messages from a durable queue. You need to:

1. Implement a message type and handler. The handler implements `IPayloadBackgroundWorker<T>`:

```csharp
public record SampleMessage(string Content);

public class SampleMessageHandler(ILogger<SampleMessageHandler> logger) : IPayloadBackgroundWorker<SampleMessage>
{
    private readonly ILogger<SampleMessageHandler> _logger = logger;

    public async Task ExecuteAsync(SampleMessage payload, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing message: {message}", payload.Content);
        // Your business logic here
        await Task.Delay(Random.Shared.Next(2, 7) * 1000, cancellationToken);
    }
}
```

2. Implement a `DurableQueue` abstraction for your storage mechanism (database, cloud queues, etc.). See [SqLiteDurableQueue](WebDemo/SqLiteDurableQueue.cs) and [DurableQueue](Abstractions/Queues/DurableQueue.cs) for reference implementations.

3. Register the durable queue and queue consumer at startup in `Program.cs`:

```csharp
builder.Services
    .AddDurableQueue(sp => new SqLiteDurableQueue("queue.db"))
    .AddQueueConsumer<SampleMessage, SampleMessageHandler>();
```

The `AddDurableQueue` method registers your queue implementation in the DI container, making it available for injection throughout your application. You can then inject it wherever needed:

```csharp
public class SomeService(DurableQueue queue)
{
    public async Task DoSomethingAsync()
    {
        await queue.EnqueueAsync(new SampleMessage("Hello"), "user-id");
    }
}
```

4. Enqueue messages in your application code:

```csharp
@inject DurableQueue Queue

<button @onclick="Enqueue">Enqueue</button>

@code {
    private async Task Enqueue()
    {        
        await Queue.EnqueueAsync(new SampleMessage("Hello world"), "current-user");
    }
}
```

**Key Features:**
- Batch dequeuing with configurable batch size
- Automatic deserialization of messages based on stored type information
- Error handling via the `OnMessageFailedAsync` hook for custom retry or dead-letter logic
- Performance tracking via `IQueueConsumerPerformance` (consume rate per configurable time window)
- Configurable delays: `EmptyQueueDelay` (how long to wait when queue is empty) and `ProcessingDelay` (delay between batches)
- Database-agnostic design: implement queue dequeue logic using platform-specific best practices (e.g., SQL Server's `DELETE ... OUTPUT`, Postgres's `FOR UPDATE SKIP LOCKED`, etc.)

# Using ScheduledBackgroundService

[ScheduledBackgroundService](Abstractions/ScheduledBackgroundService.cs) runs jobs on a schedule. Create a class that derives from `ManagedBackgroundService` and performs one scheduled execution each time `ExecuteInternalAsync` runs:

```csharp
public class DbCleanupJob(ILoggerFactory loggerFactory) : ManagedBackgroundService(loggerFactory)
{
    protected override async Task ExecuteInternalAsync(CancellationToken stoppingToken)
    {        
        Logger.LogInformation("Running database cleanup job");
        await Task.Delay(100, stoppingToken);
    }
}
```

Register your scheduled jobs at startup using `RecurrencePattern` expressions:

```csharp
builder.Services.AddScheduledJobs(schedule =>
{
    schedule.Add<DbCleanupJob>("*1d t[2:00am]");
    schedule.Add<ReindexJob>("d[mon..fri] t[9:30am, 3:30pm]");
    schedule.Add<WeeklyReportsJob>("d[sat]");
    schedule.Add<FrequentJob>("*5s");
});
```

**RecurrencePattern Examples:**
- `*1d t[2:00am]` = Daily at 2:00 AM (UTC)
- `d[mon..fri] t[9:30am, 3:30pm]` = Monday through Friday at 9:30 AM and 3:30 PM
- `d[sat]` = Every Saturday at midnight (UTC)
- `*5s` = Every 5 seconds
- `*90min b[7:30, 15:30]` = Every 90 minutes between 7:30 AM and 3:30 PM UTC
- `d[mon..fri] t[9:30am, 3:30pm] tz:America/New_York` = Monday through Friday at 9:30 AM and 3:30 PM, eastern time

For more details on `RecurrencePattern` syntax, see the [tests](Testing/RecurrencePatternTests.cs).

# Logging Features
An in-memory logger [InMemoryLogger](Abstractions/Logging/InMemoryLogger.cs) is added when you add background jobs to your service collection. This gives you access to your jobs' recent activity without relying on a particular observability solution. Any other logging providers you've configured will still work. The [Dashboard](RCL/Dashboard.razor) Blazor component presents running job info with related logs using the [IInMemoryLogQuery](Abstractions/Logging/IInMemoryLogQuery.cs) interface, which offers a few common log queries.

# Razor Class Library
The [RCL](/RCL/RCL.csproj) project has components for Blazor Server apps for managing jobs, in particular:
- [Dashboard](RCL/Dashboard.razor) used here in the [demo page](WebDemo/Components/Pages/Home.razor)

![img](dashboard-demo.png)

