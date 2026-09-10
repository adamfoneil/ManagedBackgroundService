There are a wealth background job frameworks and libraries out there -- Hangfire, Quartz, and Coravel come to mind. ASP.NET Core has its own [BackgroundService](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0&tabs=visual-studio#backgroundservice-base-class). As a matter of preference, I happen to like Coravel. But more than that, I like the flexibility that the native BackgroundService offers. But the problem with BackgroundService is its very flexibility. It's a bare-minimum service shell that I have found too easy to mis-use. This project, therefore, is a thin wrapper around the native BackgroundService, adding a few features to give you more of a "pit of success."

First off, what makes BackgroundService hard to use? When you create an empty BackgroundService and create its default implementation, you start with something like this:

```csharp
internal class SampleBackgroundService : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        throw new NotImplementedException();
    }
}
```

The problem with this is it's not clear what pattern to implement to give you a service that can be stopped and started at will, or for that matter scheduled -- that can handle errors and not cause problems for the rest of your app. There's a core `while` loop you need along with some carefully positioned `try/catch` blocks along with a little bit of state management. It's not obvious at all from the default boilerplate what this should look like.

That's where [ManagedBackgroundService](ManagedBackgroundService.Abstractions/ManagedBackgroundService.cs) comes in. It's an abstract class just like the native BackgroundService. The work your service does goes in the `ExecuteInternalAsync` method. There are `Pause` and `Resume` methods that do what they sound like, along with a `Status` property that returns Running, Paused, or Crashed. If your `ExecuteInternalAsync` throws an exception, the service goes into a Paused state. A paused job can be resumed. If an exception occurs outside the inner `try` block, the job goes into a Crashed state. If that happens, it can't be resumed, and you must restart the host app.