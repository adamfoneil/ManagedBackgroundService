namespace ManagedBackgroundServices.Abstractions.Scheduling;

/// <summary>
/// Registry for scheduled job handlers with their recurrence patterns.
/// Stores handlers and their scheduling information for a single scheduled job runner.
/// </summary>
public class ScheduledJobHandlerRegistry
{
    private readonly Dictionary<string, ScheduledJobHandler> _handlers = [];

    /// <summary>
    /// Represents a scheduled job handler with its recurrence pattern.
    /// </summary>
    public class ScheduledJobHandler
    {
        public required string Name { get; init; }
        public required Func<CancellationToken, Task> Handler { get; init; }
        public required RecurrencePattern Pattern { get; init; }
        public DateTimeOffset NextRunTime { get; set; }
    }

    /// <summary>
    /// Registers a scheduled job handler with a name and recurrence pattern.
    /// </summary>
    /// <param name="name">The handler name</param>
    /// <param name="handler">The async handler to execute</param>
    /// <param name="patternString">The recurrence pattern string (parsed via RecurrencePattern.Parse)</param>
    public void Add(string name, Func<CancellationToken, Task> handler, string patternString)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Handler name cannot be null or empty.", nameof(name));
        if (handler is null)
            throw new ArgumentNullException(nameof(handler));
        if (string.IsNullOrWhiteSpace(patternString))
            throw new ArgumentException("Pattern string cannot be null or empty.", nameof(patternString));

        var pattern = RecurrencePattern.Parse(patternString);

        _handlers[name] = new ScheduledJobHandler
        {
            Name = name,
            Handler = handler,
            Pattern = pattern,
            NextRunTime = DateTimeOffset.MinValue
        };
    }

    /// <summary>
    /// Gets all registered handlers.
    /// </summary>
    /// <returns>A collection of all registered handlers</returns>
    public IReadOnlyCollection<ScheduledJobHandler> GetHandlers() => _handlers.Values;

    /// <summary>
    /// Gets a handler by name.
    /// </summary>
    /// <param name="name">The handler name</param>
    /// <param name="handler">The handler, or null if not found</param>
    /// <returns>True if the handler was found; false otherwise</returns>
    public bool TryGetHandler(string name, out ScheduledJobHandler? handler)
    {
        return _handlers.TryGetValue(name, out handler);
    }

    /// <summary>
    /// Gets the next handler to run based on current time.
    /// </summary>
    /// <param name="currentTime">The current time</param>
    /// <returns>The handler that should run next, or null if no handlers are ready</returns>
    public ScheduledJobHandler? GetNextHandlerToRun(DateTimeOffset currentTime)
    {
        return _handlers.Values
            .Where(h => h.NextRunTime <= currentTime)
            .OrderBy(h => h.NextRunTime)
            .FirstOrDefault();
    }

    /// <summary>
    /// Gets the next wake-up time across all handlers.
    /// </summary>
    /// <param name="currentTime">The current time</param>
    /// <returns>The earliest next run time across all handlers, or null if no handlers exist</returns>
    public DateTimeOffset? GetNextWakeUpTime(DateTimeOffset currentTime)
    {
        var nextTimes = _handlers.Values
            .Select(h => h.NextRunTime > currentTime ? h.NextRunTime : h.Pattern.GetNextOccurrence(currentTime))
            .ToList();

        return nextTimes.Count == 0 ? null : nextTimes.Min();
    }
}
