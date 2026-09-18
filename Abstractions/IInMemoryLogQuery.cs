namespace ManagedBackgroundServices.Abstractions;

public interface IInMemoryLogQuery
{
    IReadOnlyList<LogEntry> GetLogs(string? category = null, int maxResults = 0, int minutesBack = 0, Func<LogEntry, bool>? criteria = null);
    //todo: health snapshot method, total recent errors, or something
}
