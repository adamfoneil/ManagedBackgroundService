namespace ScheduleAbstractions;

public interface IRecurrencePattern
{
    DateTimeOffset GetNextOccurrence(DateTimeOffset after);
}
