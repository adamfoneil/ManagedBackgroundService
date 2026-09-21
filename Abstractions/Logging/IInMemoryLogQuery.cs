using Microsoft.Extensions.Logging;

namespace ManagedBackgroundServices.Abstractions.Logging;

public interface IInMemoryLogQuery
{
    IReadOnlyList<LogEntry> GetLogs(string? category = null, int maxResults = 0, int minutesBack = 0, Func<LogEntry, bool>? criteria = null);

    ILookup<string, LogEntry> GetLogsByCategory(int maxPerCategory = 0, int minutesBack = 0, Func<LogEntry, bool>? criteria = null)
    {
        var logs = GetLogs(minutesBack: minutesBack, criteria: criteria);

        if (maxPerCategory <= 0)
        {
            return logs.ToLookup(e => e.Category);
        }

        return logs
            .GroupBy(e => e.Category)
            .SelectMany(g => g.Take(maxPerCategory))
            .ToLookup(e => e.Category);
    }

    Dictionary<(string Category, LogLevel Level), int> GetCategorySnapshot(int minutesBack = 0, Func<LogEntry, bool>? criteria = null) => GetLogs(minutesBack: minutesBack, criteria: criteria).CountBy(e => (e.Category, e.Level)).ToDictionary();

    Dictionary<(string Category, LogLevel Level), int> GetErrorSnapshot(int minutesBack = 0) => GetCategorySnapshot(minutesBack: minutesBack, criteria: (e) => e.Level >= LogLevel.Error);

    ILookup<string, (LogLevel Level, int Count)> GetErrorsByCategory(int minutesBack = 0) => GetErrorSnapshot(minutesBack: minutesBack).ToLookup(grp => grp.Key.Category, grp => (grp.Key.Level, grp.Value));

    IReadOnlyList<LogEntry> GetLogsByHandler(string handlerName, int maxResults = 0, int minutesBack = 0, Func<LogEntry, bool>? criteria = null)
    {
        Func<LogEntry, bool> handlerFilter = e => e.HandlerName == handlerName;
        var combinedCriteria = criteria == null 
            ? handlerFilter 
            : e => handlerFilter(e) && criteria(e);
        return GetLogs(maxResults: maxResults, minutesBack: minutesBack, criteria: combinedCriteria);
    }

    ILookup<string?, LogEntry> GetLogsByHandlerName(int maxResults = 0, int minutesBack = 0, Func<LogEntry, bool>? criteria = null)
    {
        var logs = GetLogs(minutesBack: minutesBack, criteria: criteria);

        if (maxResults <= 0)
        {
            return logs.ToLookup(e => e.HandlerName);
        }

        return logs
            .GroupBy(e => e.HandlerName)
            .SelectMany(g => g.Take(maxResults))
            .ToLookup(e => e.HandlerName);
    }

    Dictionary<(string? HandlerName, LogLevel Level), int> GetHandlerSnapshot(int minutesBack = 0, Func<LogEntry, bool>? criteria = null) => GetLogs(minutesBack: minutesBack, criteria: criteria).CountBy(e => (e.HandlerName, e.Level)).ToDictionary();

    Dictionary<(string? HandlerName, LogLevel Level), int> GetHandlerErrorSnapshot(int minutesBack = 0) => GetHandlerSnapshot(minutesBack: minutesBack, criteria: (e) => e.Level >= LogLevel.Error);
}
