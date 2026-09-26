# Bug Fix Summary: SampleMessageHandler Logs Not Showing in Dashboard

## Problem
Messages logged by `SampleMessageHandler` were not appearing in the Dashboard.razor view, despite the handler being registered and executing correctly.

## Root Cause
The Dashboard uses `LogQuery.GetLogsByHandlerName()` to retrieve logs, which extracts the `HandlerName` from log entry scopes. However, `QueueConsumerBackgroundService<T>` was not creating a scope with the handler name when invoking `IPayloadBackgroundWorker<T>`.

**Comparison:**
- ✅ `ScheduledBackgroundService` (line 53): Creates scope with `HandlerName` before executing worker
- ❌ `QueueConsumerBackgroundService` (original): Did NOT create scope when calling handler

Without the scope, logs from `SampleMessageHandler` had:
- **Category**: "WebDemo.BackgroundJobs.SampleMessageHandler" (full class name)
- **HandlerName**: `null` (not set in scopes)

The Dashboard expected to find logs grouped by `HandlerName`, so the handler's logs were invisible.

## Solution
Added a logging scope in `QueueConsumerBackgroundService<T>.ExecuteInternalAsync()` (line 97-100) that wraps the handler execution:

```csharp
using (Logger.BeginScope(new Dictionary<string, object> { { "HandlerName", HandlerIdentifier } }))
{
	await _handler.ExecuteAsync(msgObject, stoppingToken);
}
```

This ensures that all logs from `SampleMessageHandler` are captured with the `HandlerName` scope, making them visible in the Dashboard with other background service logs.

## Impact
- ✅ Logs from queue message handlers now appear in Dashboard.razor
- ✅ Consistent logging behavior between scheduled jobs and queue handlers
- ✅ No breaking changes; only internal logging behavior improved

## Files Modified
- `Abstractions/Queues/QueueConsumerBackgroundService.cs`
