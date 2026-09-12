namespace ScheduleAbstractions;

public class WeeklyRecurrencePattern : IRecurrencePattern
{
    public string? TimeZoneId { get; init; }
    public DayOfWeek[] Days { get; init; } = [];
    public TimeOnly[] Times { get; init; } = [];

    public static WeeklyRecurrencePattern FromExpression(string expression)
    {
        throw new NotImplementedException();
    }

    public DateTimeOffset GetNextOccurrence(DateTimeOffset after)
    {
        if (Days.Length == 0)
        {
            throw new InvalidOperationException("At least one day must be configured.");
        }

        if (Times.Length == 0)
        {
            throw new InvalidOperationException("At least one time must be configured.");
        }

        var timeZone = ResolveTimeZone();
        var localAfter = TimeZoneInfo.ConvertTime(after, timeZone);
        var orderedDays = Days.Distinct().OrderBy(day => ((int)day + 1) % 7).ToArray();
        var orderedTimes = Times.Distinct().OrderBy(time => time.Ticks).ToArray();

        for (var dayOffset = 0; dayOffset <= 7; dayOffset++)
        {
            var candidateDate = localAfter.Date.AddDays(dayOffset);
            if (!orderedDays.Contains(candidateDate.DayOfWeek))
            {
                continue;
            }

            foreach (var time in orderedTimes)
            {
                var localCandidate = candidateDate.Add(time.ToTimeSpan());
                var candidate = new DateTimeOffset(localCandidate, timeZone.GetUtcOffset(localCandidate));

                if (candidate > after)
                {
                    return candidate;
                }
            }
        }

        throw new InvalidOperationException("No future occurrence exists for this pattern.");
    }

    private TimeZoneInfo ResolveTimeZone() =>
        string.IsNullOrWhiteSpace(TimeZoneId)
            ? TimeZoneInfo.Local
            : TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
}
