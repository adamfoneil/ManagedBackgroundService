namespace ManagedBackgroundServices.Abstractions;

public interface IInMemoryLogQuery
{
    IReadOnlyList<LogEntry> GetLogs(int maxResults = 0, int minutesBack = 0, Func<LogEntry, bool>? criteria = null);
}
