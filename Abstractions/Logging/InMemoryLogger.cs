using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ManagedBackgroundServices.Abstractions.Logging;

/// <summary>
/// Represents a single log entry in the in-memory ring buffer
/// </summary>
public class LogEntry
{
    public DateTime TimestampUtc { get; set; }
    public TimeSpan GetAge(DateTime from) => from.Subtract(TimestampUtc);
    public LogLevel Level { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
}

/// <summary>
/// An in-memory circular buffer logger provider for background jobs.
/// Holds only recent log entries, perfect for enterprise environments without external observability tools.
/// Thread-safe and bounded memory usage.
/// </summary>
public sealed class InMemoryLoggerProvider : ILoggerProvider, IInMemoryLogQuery
{
    private static readonly string[] ExcludedCategoryPrefixes = ["Microsoft.", "System."];

    private readonly int _maxCapacity;
    private readonly ConcurrentQueue<LogEntry> _buffer = new();
    private readonly Lock _lockObj = new();

    /// <summary>
    /// Creates a new InMemoryLoggerProvider
    /// </summary>
    /// <param name="maxCapacity">Maximum number of log entries to keep (default: 1000)</param>
    public InMemoryLoggerProvider(int maxCapacity = 1000)
    {
        if (maxCapacity <= 0) throw new ArgumentException("Capacity must be greater than zero", nameof(maxCapacity));
        _maxCapacity = maxCapacity;
    }

    public ILogger CreateLogger(string categoryName) =>
        new InMemoryLogger(categoryName, this);

    public void Dispose() { }

    /// <summary>
    /// Adds a log entry to the ring buffer, removing oldest if at capacity
    /// </summary>
    internal void LogMessage(LogEntry entry)
    {
        if (IsExcludedCategory(entry.Category))
        {
            return;
        }

        lock (_lockObj)
        {
            _buffer.Enqueue(entry);
            while (_buffer.Count > _maxCapacity && _buffer.TryDequeue(out _)) { }
        }
    }

    private static bool IsExcludedCategory(string category) =>
        ExcludedCategoryPrefixes.Any(prefix => category.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>
    /// Clears all log entries
    /// </summary>
    public void Clear()
    {
        lock (_lockObj)
        {
            while (_buffer.TryDequeue(out _)) { }
        }
    }

    public IReadOnlyList<LogEntry> GetLogs(string? category = null, int maxResults = 0, int minutesBack = 0, Func<LogEntry, bool>? criteria = null)
    {
        // Capture a snapshot of entries to avoid holding locks while filtering
        var snapshot = _buffer.ToArray();

        DateTime? cutoff = minutesBack > 0 ? DateTime.UtcNow.AddMinutes(-minutesBack) : null;

        var query = snapshot.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(e => e.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        if (cutoff.HasValue)
        {
            query = query.Where(e => e.TimestampUtc >= cutoff.Value);
        }

        if (criteria is not null)
        {
            query = query.Where(criteria);
        }

        // Order newest first
        query = query.OrderByDescending(e => e.TimestampUtc);

        if (maxResults > 0)
        {
            query = query.Take(maxResults);
        }

        return query.ToList().AsReadOnly();
    }

    private class InMemoryLogger(string categoryName, InMemoryLoggerProvider provider) : ILogger
    {
        private readonly string _categoryName = categoryName;
        private readonly InMemoryLoggerProvider _provider = provider;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var now = DateTime.UtcNow;

            var entry = new LogEntry
            {
                TimestampUtc = now,                
                Level = logLevel,
                Category = _categoryName,
                Message = formatter(state, exception),
                Exception = exception
            };

            _provider.LogMessage(entry);
        }
    }
}