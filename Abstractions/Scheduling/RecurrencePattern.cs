using System.Globalization;
using System.Text.RegularExpressions;

namespace ManagedBackgroundServices.Abstractions.Scheduling;

public sealed class RecurrencePattern
{
    public string TimeZoneId { get; init; } = "UTC";
    public DayOfWeek[] WeekDays { get; init; } = [];
    /// <summary>
    /// postive values count from start of month, negative values count from end of month
    /// </summary>
    public int[] MonthDays { get; init; } = [];
    /// <summary>
    /// positive values count from start of year, negative values count from end of year
    /// </summary>
    public int[] YearDays { get; init; } = [];
    /// <summary>
    /// midnight by default
    /// </summary>
    public TimeOnly[] Times { get; init; } = [new(0, 0)];
    public TimeSpan? Period { get; init; }
    public (TimeOnly Start, TimeOnly End)? Between { get; init; }

    private static readonly Regex TokenRegex = new(
        @"(?ix)(?:
            tz\s*:\s*(?<tz>[^\s]+)
            | (?<period>\*\s*(?<periodValue>\d+)\s*(?<periodUnit>sec|s|min|m|hr|h|day|d))
            | (?<between>between)\s*\[(?<betweenValue>[^\]]*)\]
            | (?<type>[bdwmyt])\s*\[(?<value>[^\]]*)\]
        )");

    private static Dictionary<DayOfWeek, string[]> DayAbbreviations => new()
    {
        [DayOfWeek.Sunday] = ["s", "sun"],
        [DayOfWeek.Monday] = ["m", "mon"],
        [DayOfWeek.Tuesday] = ["t", "tue"],
        [DayOfWeek.Wednesday] = ["w", "wed"],
        [DayOfWeek.Thursday] = ["r", "thr", "thu"],
        [DayOfWeek.Friday] = ["f", "fri"],
        [DayOfWeek.Saturday] = ["a", "sat"]
    };

    private static Dictionary<string, DayOfWeek> DaysByAbbreviation =>
        DayAbbreviations
            .SelectMany(kp => kp.Value.Select(abbrev => new { Abbrev = abbrev, Day = kp.Key }))
            .GroupBy(x => x.Abbrev, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Day, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// can be like
    /// d[0,3,6] = sun, tue, sat (WeeklyDays), d[tue..sat] = Tuesday through Saturday
    /// m[-1] = last day of month, m[1, 15] = first and fifteenth (MonthlyDays), 
    /// y[7, -7] seventh day of the year, seven days from end of year (YearlyDays)
    /// t[9:30, 18:30] = 9:30AM, 6:30PM
    /// EST (optional TimeZone = Eastern Standard Time or any standard abbreviation)
    /// </summary>
    public static RecurrencePattern Parse(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new ArgumentException("Expression cannot be empty.", nameof(expression));
        }

        var matches = TokenRegex.Matches(expression);
        if (matches.Count == 0)
        {
            throw new FormatException($"Unable to parse recurrence expression '{expression}'.");
        }

        var timeZoneId = "UTC";
        DayOfWeek[] weekDays = [];
        int[] monthDays = [];
        int[] yearDays = [];
        TimeOnly[] times = [new(0, 0)];
        TimeSpan? period = null;
        (TimeOnly Start, TimeOnly End)? between = null;

        var seenWeekDays = false;
        var seenMonthDays = false;
        var seenYearDays = false;
        var seenTime = false;
        var lastIndex = 0;

        foreach (Match match in matches)
        {
            var leadingGap = expression[lastIndex..match.Index];
            if (!string.IsNullOrWhiteSpace(leadingGap))
            {
                throw new FormatException($"Unexpected text in recurrence expression: '{leadingGap.Trim()}'.");
            }

            var timeZoneName = match.Groups["tz"].Value;
            if (!string.IsNullOrWhiteSpace(timeZoneName))
            {
                timeZoneId = timeZoneName;
                lastIndex = match.Index + match.Length;
                continue;
            }

            var periodText = match.Groups["period"].Value;
            if (!string.IsNullOrWhiteSpace(periodText))
            {
                period = ParsePeriodToken(periodText);
                lastIndex = match.Index + match.Length;
                continue;
            }

            var betweenText = match.Groups["between"].Value;
            var betweenValue = match.Groups["betweenValue"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(betweenText) || (!string.IsNullOrWhiteSpace(match.Groups["type"].Value) && match.Groups["type"].Value.Equals("b", StringComparison.OrdinalIgnoreCase)))
            {
                between = ParseBetweenValue(!string.IsNullOrWhiteSpace(betweenValue) ? betweenValue : match.Groups["value"].Value.Trim());
                lastIndex = match.Index + match.Length;
                continue;
            }

            var selectorType = match.Groups["type"].Value;
            var selectorValue = match.Groups["value"].Value.Trim();

            switch (selectorType.ToLowerInvariant())
            {
                case "d":
                case "w":
                    if (seenWeekDays || seenMonthDays || seenYearDays)
                    {
                        throw new FormatException("Cannot combine weekday, month-day, and year-day selectors in the same recurrence pattern.");
                    }

                    weekDays = ParseWeekDays(selectorValue);
                    seenWeekDays = true;
                    break;
                case "m":
                    if (seenWeekDays || seenMonthDays || seenYearDays)
                    {
                        throw new FormatException("Cannot combine weekday, month-day, and year-day selectors in the same recurrence pattern.");
                    }

                    monthDays = ParseIntegerValues(selectorValue, 1, 31, -31, true);
                    seenMonthDays = true;
                    break;
                case "y":
                    if (seenWeekDays || seenMonthDays || seenYearDays)
                    {
                        throw new FormatException("Cannot combine weekday, month-day, and year-day selectors in the same recurrence pattern.");
                    }

                    yearDays = ParseIntegerValues(selectorValue, 1, 366, -366, true);
                    seenYearDays = true;
                    break;
                case "t":
                    if (seenTime)
                    {
                        throw new FormatException("A recurrence pattern can only declare one time selector.");
                    }

                    times = ParseTimes(selectorValue);
                    seenTime = true;
                    break;
                case "b":
                    between = ParseBetweenValue(selectorValue);
                    break;
                default:
                    throw new FormatException($"Unknown recurrence selector '{selectorType}'.");
            }

            lastIndex = match.Index + match.Length;
        }

        var trailingGap = expression[lastIndex..];
        if (!string.IsNullOrWhiteSpace(trailingGap))
        {
            throw new FormatException($"Unexpected trailing text in recurrence expression: '{trailingGap.Trim()}'.");
        }

        return new RecurrencePattern
        {
            TimeZoneId = timeZoneId,
            WeekDays = weekDays,
            MonthDays = monthDays,
            YearDays = yearDays,
            Times = times,
            Period = period,
            Between = between
        };
    }

    public DateTimeOffset GetNextOccurrence(DateTimeOffset after)
    {
        var timeZone = ResolveTimeZone(TimeZoneId);
        var localAfter = TimeZoneInfo.ConvertTime(after, timeZone);

        if (Period.HasValue)
        {
            return GetNextPeriodOccurrence(after, timeZone, localAfter, Period.Value);
        }

        if (WeekDays.Length > 0)
        {
            var orderedDays = WeekDays.Distinct().OrderBy(day => (int)day).ToArray();
            var candidateDates = Enumerable.Range(0, 15)
                .Select(offset => localAfter.Date.AddDays(offset))
                .Where(date => orderedDays.Contains(date.DayOfWeek));

            var nextOccurrence = TryGetNextOccurrence(after, timeZone, localAfter, candidateDates);
            if (nextOccurrence.HasValue)
            {
                return nextOccurrence.Value;
            }
        }

        if (MonthDays.Length > 0)
        {
            var orderedDays = MonthDays.Distinct().OrderBy(day => day).ToArray();
            var candidateDates = GetMonthlyCandidateDates(localAfter, orderedDays);

            var nextOccurrence = TryGetNextOccurrence(after, timeZone, localAfter, candidateDates);
            if (nextOccurrence.HasValue)
            {
                return nextOccurrence.Value;
            }
        }

        if (YearDays.Length > 0)
        {
            var orderedDays = YearDays.Distinct().OrderBy(day => day).ToArray();
            var candidateDates = GetYearlyCandidateDates(localAfter, orderedDays);

            var nextOccurrence = TryGetNextOccurrence(after, timeZone, localAfter, candidateDates);
            if (nextOccurrence.HasValue)
            {
                return nextOccurrence.Value;
            }
        }

        var defaultTime = Times.OrderBy(time => time.Ticks).First();
        var nextDate = localAfter.Date.Add(defaultTime.ToTimeSpan());
        if (nextDate <= localAfter)
        {
            nextDate = nextDate.AddDays(1);
        }

        return new DateTimeOffset(nextDate, timeZone.GetUtcOffset(nextDate));
    }

    private DateTimeOffset? TryGetNextOccurrence(DateTimeOffset after, TimeZoneInfo timeZone, DateTimeOffset localAfter, IEnumerable<DateTime> candidateDates)
    {
        var orderedTimes = Times.Distinct().OrderBy(time => time.Ticks).ToArray();

        foreach (var date in candidateDates)
        {
            foreach (var time in orderedTimes)
            {
                var candidateLocal = date.Add(time.ToTimeSpan());
                if (candidateLocal <= localAfter)
                {
                    continue;
                }

                var candidateUtc = new DateTimeOffset(candidateLocal, timeZone.GetUtcOffset(candidateLocal));
                if (candidateUtc > after)
                {
                    return candidateUtc;
                }
            }
        }

        return null;
    }

    private DateTimeOffset GetNextPeriodOccurrence(DateTimeOffset after, TimeZoneInfo timeZone, DateTimeOffset localAfter, TimeSpan period)
    {
        if (period <= TimeSpan.Zero)
        {
            throw new FormatException("Period must be greater than zero.");
        }

        if (!Between.HasValue)
        {
            return after.Add(period);
        }

        var candidate = localAfter.Add(period);
        var searchDate = candidate.Date;

        for (var dayOffset = 0; dayOffset < 367; dayOffset++)
        {
            var windowDate = searchDate.AddDays(dayOffset);
            var (windowStart, windowEnd) = GetWindowBounds(windowDate, timeZone, Between.Value);

            if (candidate < windowStart)
            {
                return windowStart;
            }

            if (candidate >= windowStart && candidate <= windowEnd)
            {
                return new DateTimeOffset(candidate.DateTime, timeZone.GetUtcOffset(candidate.DateTime));
            }

            candidate = windowStart;
        }

        throw new InvalidOperationException("Unable to compute next period occurrence within the supported search window.");
    }

    private static (DateTimeOffset Start, DateTimeOffset End) GetWindowBounds(DateTime windowDate, TimeZoneInfo timeZone, (TimeOnly Start, TimeOnly End) between)
    {
        var startLocal = windowDate.Date.Add(between.Start.ToTimeSpan());
        var endLocal = between.End >= between.Start
            ? windowDate.Date.Add(between.End.ToTimeSpan())
            : windowDate.Date.AddDays(1).Add(between.End.ToTimeSpan());

        return (
            new DateTimeOffset(startLocal, timeZone.GetUtcOffset(startLocal)),
            new DateTimeOffset(endLocal, timeZone.GetUtcOffset(endLocal)));
    }

    private static IEnumerable<DateTime> GetMonthlyCandidateDates(DateTimeOffset localAfter, int[] orderedDays)
    {
        return Enumerable.Range(0, 25)
            .Select(monthOffset => localAfter.Date.AddMonths(monthOffset))
            .SelectMany(candidateMonth => GetValidDatesForMonth(candidateMonth, orderedDays));
    }

    private static IEnumerable<DateTime> GetYearlyCandidateDates(DateTimeOffset localAfter, int[] orderedDays)
    {
        return Enumerable.Range(0, 6)
            .Select(yearOffset => localAfter.Year + yearOffset)
            .SelectMany(year => GetValidDatesForYear(year, orderedDays));
    }

    private static IEnumerable<DateTime> GetValidDatesForMonth(DateTime monthDate, int[] orderedDays)
    {
        var totalDays = DateTime.DaysInMonth(monthDate.Year, monthDate.Month);
        return orderedDays
            .Select(day => day > 0 ? day : totalDays + day + 1)
            .Where(actualDay => actualDay >= 1 && actualDay <= totalDays)
            .Select(actualDay => new DateTime(monthDate.Year, monthDate.Month, actualDay));
    }

    private static IEnumerable<DateTime> GetValidDatesForYear(int year, int[] orderedDays)
    {
        var totalDays = DateTime.IsLeapYear(year) ? 366 : 365;
        return orderedDays
            .Select(dayOfYear => dayOfYear > 0 ? dayOfYear : totalDays + dayOfYear + 1)
            .Where(actualDay => actualDay >= 1 && actualDay <= totalDays)
            .Select(actualDay => new DateTime(year, 1, 1).AddDays(actualDay - 1));
    }

    private static TimeSpan ParsePeriodToken(string value)
    {
        var token = value.Trim();
        if (token.StartsWith('*'))
        {
            token = token[1..].Trim();
        }

        var match = Regex.Match(token, @"^(?<value>\d+)\s*(?<unit>sec|s|min|m|hr|h|day|d)$", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            throw new FormatException($"Invalid period token '{value}'.");
        }

        var multiplier = int.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
        var unit = match.Groups["unit"].Value.ToLowerInvariant();

        return unit switch
        {
            "sec" or "s" => TimeSpan.FromSeconds(multiplier),
            "min" or "m" => TimeSpan.FromMinutes(multiplier),
            "hr" or "h" => TimeSpan.FromHours(multiplier),
            "day" or "d" => TimeSpan.FromDays(multiplier),
            _ => throw new FormatException($"Unsupported period unit '{unit}'.")
        };
    }

    private static (TimeOnly Start, TimeOnly End)? ParseBetweenValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException("Between selector cannot be empty.");
        }

        var commaParts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (commaParts.Length == 2)
        {
            return (ParseTimeToken(commaParts[0]), ParseTimeToken(commaParts[1]));
        }

        var rangeParts = value.Split("..", 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (rangeParts.Length == 2)
        {
            return (ParseTimeToken(rangeParts[0]), ParseTimeToken(rangeParts[1]));
        }

        throw new FormatException($"Between selector '{value}' must contain exactly two time values.");
    }

    private static DayOfWeek[] ParseWeekDays(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        if (value.Equals("*", StringComparison.OrdinalIgnoreCase))
        {
            return Enum.GetValues<DayOfWeek>();
        }

        var values = new List<DayOfWeek>();
        foreach (var part in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Contains("..", StringComparison.Ordinal))
            {
                var bounds = part.Split("..", 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (bounds.Length != 2)
                {
                    throw new FormatException($"Invalid day range '{part}'.");
                }

                var start = ParseDayToken(bounds[0]);
                var end = ParseDayToken(bounds[1]);
                var step = start <= end ? 1 : -1;
                for (var day = (int)start; ; day += step)
                {
                    values.Add((DayOfWeek)day);
                    if (day == (int)end)
                    {
                        break;
                    }
                }

                continue;
            }

            values.Add(ParseDayToken(part));
        }

        return [.. values.Distinct()];
    }

    private static DayOfWeek ParseDayToken(string value)
    {
        var token = value.Trim();
        if (token.Length == 0)
        {
            throw new FormatException("Day token cannot be empty.");
        }

        if (int.TryParse(token, out var numericValue))
        {
            if (numericValue is < 0 or > 6)
            {
                throw new FormatException($"Day number '{token}' is outside the valid range 0-6.");
            }

            return (DayOfWeek)numericValue;
        }

        if (DaysByAbbreviation.TryGetValue(token, out var mapped))
        {
            return mapped;
        }

        throw new FormatException($"Unknown weekday token '{token}'.");
    }

    private static TimeOnly[] ParseTimes(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [new(0, 0)];
        }

        var items = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var times = new List<TimeOnly>();
        foreach (var item in items)
        {
            times.Add(ParseTimeToken(item));
        }

        return [.. times];
    }

    private static TimeOnly ParseTimeToken(string value)
    {
        var token = value.Trim();
        if (token.Length == 0)
        {
            throw new FormatException("Time token cannot be empty.");
        }

        var hasMeridiem = token.EndsWith("am", StringComparison.OrdinalIgnoreCase)
            || token.EndsWith("pm", StringComparison.OrdinalIgnoreCase);

        if (hasMeridiem)
        {
            token = token[..^2].Trim();
        }

        var parts = token.Split(':', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            throw new FormatException($"Invalid time token '{value}'.");
        }

        if (!int.TryParse(parts[0], out var hour) || !int.TryParse(parts[1], out var minute))
        {
            throw new FormatException($"Invalid time token '{value}'.");
        }

        if (minute is < 0 or > 59)
        {
            throw new FormatException($"Minute value '{minute}' is outside the valid range 0-59.");
        }

        var isPm = value.EndsWith("pm", StringComparison.OrdinalIgnoreCase);
        var isAm = value.EndsWith("am", StringComparison.OrdinalIgnoreCase);
        if (isPm)
        {
            if (hour == 12)
            {
                hour = 12;
            }
            else
            {
                hour += 12;
            }
        }
        else if (isAm && hour == 12)
        {
            hour = 0;
        }

        if (hour is < 0 or > 23)
        {
            throw new FormatException($"Hour value '{hour}' is outside the valid range 0-23.");
        }

        return new TimeOnly(hour, minute);
    }

    private static int[] ParseIntegerValues(string value, int minInclusive, int maxInclusive, int minNegativeInclusive, bool allowWildcard)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        if (allowWildcard && value.Equals("*", StringComparison.OrdinalIgnoreCase))
        {
            var values = new List<int>();
            for (var n = minInclusive; n <= maxInclusive; n++)
            {
                values.Add(n);
            }

            return [.. values];
        }

        var result = new List<int>();
        foreach (var part in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Contains("..", StringComparison.Ordinal))
            {
                var bounds = part.Split("..", 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (bounds.Length != 2)
                {
                    throw new FormatException($"Invalid numeric range '{part}'.");
                }

                var start = ParseIntegerToken(bounds[0], minInclusive, maxInclusive, minNegativeInclusive);
                var end = ParseIntegerToken(bounds[1], minInclusive, maxInclusive, minNegativeInclusive);
                var step = start <= end ? 1 : -1;
                for (var number = start; ; number += step)
                {
                    result.Add(number);
                    if (number == end)
                    {
                        break;
                    }
                }

                continue;
            }

            result.Add(ParseIntegerToken(part, minInclusive, maxInclusive, minNegativeInclusive));
        }

        return [.. result.Distinct()];
    }

    private static int ParseIntegerToken(string token, int minInclusive, int maxInclusive, int minNegativeInclusive)
    {
        if (!int.TryParse(token, out var value))
        {
            throw new FormatException($"Unexpected integer token '{token}'.");
        }

        if (value >= 0)
        {
            if (value < minInclusive || value > maxInclusive)
            {
                throw new FormatException($"Integer '{value}' is outside the valid range {minInclusive}-{maxInclusive}.");
            }

            return value;
        }

        if (value < minNegativeInclusive || value > -1)
        {
            throw new FormatException($"Integer '{value}' is outside the valid negative range {minNegativeInclusive}..-1.");
        }

        return value;
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId) || string.Equals(timeZoneId, "UTC", StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            foreach (var zone in TimeZoneInfo.GetSystemTimeZones())
            {
                if (zone.Id.Equals(timeZoneId, StringComparison.OrdinalIgnoreCase)
                    || zone.StandardName.Equals(timeZoneId, StringComparison.OrdinalIgnoreCase)
                    || zone.DisplayName.Contains(timeZoneId, StringComparison.OrdinalIgnoreCase))
                {
                    return zone;
                }
            }

            return TimeZoneInfo.Utc;
        }
    }
}
