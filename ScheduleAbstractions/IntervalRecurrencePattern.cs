namespace ScheduleAbstractions;

public class IntervalRecurrencePattern : IRecurrencePattern
{
    public TimeSpan Interval { get; init; }

    public DateTimeOffset GetNextOccurrence(DateTimeOffset after)
    {
        if (Interval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Interval must be greater than zero.");
        }

        return after.Add(Interval);
    }
}
