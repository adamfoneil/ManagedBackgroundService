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

    Dictionary<(string, LogLevel), int> GetCategorySnapshot(int minutesBack = 0, Func<LogEntry, bool>? criteria = null) => GetLogs(minutesBack: minutesBack, criteria: criteria).CountBy(e => (e.Category, e.Level)).ToDictionary();

    Dictionary<(string, LogLevel), int> GetErrorSnapshot(int minutesBack = 0) => GetCategorySnapshot(minutesBack: minutesBack, criteria: (e) => e.Level >= LogLevel.Error);
}
