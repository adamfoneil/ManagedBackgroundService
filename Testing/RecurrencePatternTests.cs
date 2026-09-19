using ManagedBackgroundServices.Abstractions.Infrastructure;

namespace Testing;

[TestClass]
public sealed class RecurrencePatternTests
{
    [TestMethod]
    public void DailyCase_Select()
    {
        var pattern = RecurrencePattern.Parse("d[tue, fri] t[9:30, 18:30] tz:America/New_York");
        Assert.IsTrue(pattern.WeekDays.SequenceEqual([DayOfWeek.Tuesday, DayOfWeek.Friday]));
        Assert.IsTrue(pattern.Times.SequenceEqual([new(9, 30), new(18, 30)]));
        Assert.AreEqual("America/New_York", pattern.TimeZoneId);
    }

    [TestMethod]
    public void DailyCase_Range()
    {
        // can use 'd' or 'w' to mean WeeklyDays
        var pattern = RecurrencePattern.Parse("w[mon..fri] t[12:00] tz:America/New_York");
        Assert.IsTrue(pattern.WeekDays.SequenceEqual([DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]));
        Assert.IsTrue(pattern.Times.SequenceEqual([new(12, 0)]));
        Assert.AreEqual("America/New_York", pattern.TimeZoneId);
    }

    [TestMethod]
    public void DailyCase_AllDays_AMPM()
    {
        var pattern = RecurrencePattern.Parse("d[*] t[8:15pm, 4:30am]");
        Assert.IsTrue(pattern.WeekDays.SequenceEqual([DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday]));
        Assert.IsTrue(pattern.Times.SequenceEqual([new(20, 15), new(4, 30)]));
        Assert.AreEqual("UTC", pattern.TimeZoneId);
    }

    [TestMethod]
    public void MonthlyCase_SelectDays()
    {
        var pattern = RecurrencePattern.Parse("m[1, 15] t[12:00am]");
        Assert.IsTrue(pattern.MonthDays.SequenceEqual([1, 15]));
        Assert.IsTrue(pattern.Times.SequenceEqual([new(0, 0)]));
        Assert.AreEqual("UTC", pattern.TimeZoneId);
    }

    [TestMethod]
    public void MonthlyCase_RangeDays()
    {
        var pattern = RecurrencePattern.Parse("m[-3..-1] t[6:30] tz:Europe/Berlin");
        Assert.IsTrue(pattern.MonthDays.SequenceEqual([-3, -2, -1]));
        Assert.IsTrue(pattern.Times.SequenceEqual([new(6, 30)]));
        Assert.AreEqual("Europe/Berlin", pattern.TimeZoneId);
    }

    [TestMethod]
    public void PeriodCase()
    {
        var pattern = RecurrencePattern.Parse("*5min");
        Assert.IsTrue(pattern.Period.Equals(TimeSpan.FromMinutes(5)));
    }

    [TestMethod]
    public void PeriodCase_GetNextOccurrence_UsesPeriod()
    {
        var pattern = RecurrencePattern.Parse("*5min");
        var after = new DateTimeOffset(2026, 9, 12, 15, 0, 0, TimeSpan.Zero);

        var next = pattern.GetNextOccurrence(after);

        Assert.AreEqual(after.AddMinutes(5), next);
    }

    [TestMethod]
    public void PeriodCase_InRange()
    {
        var pattern = RecurrencePattern.Parse("*90sec between[9:30am, 2:30pm] tz:America/New_York");
        Assert.IsTrue(pattern.Period.Equals(TimeSpan.FromSeconds(90)));
        Assert.AreEqual(new(9, 30), pattern.Between!.Value.Start);
        Assert.AreEqual(new(14, 30), pattern.Between!.Value.End);
    }

    [TestMethod]
    public void PeriodCase_GetNextOccurrence_BeforeWindow_StartsAtWindowOpen()
    {
        var pattern = RecurrencePattern.Parse("*90sec between[9:30am, 2:30pm] tz:America/New_York");
        var after = new DateTimeOffset(2026, 9, 12, 13, 0, 0, TimeSpan.Zero);

        var next = pattern.GetNextOccurrence(after);

        Assert.AreEqual(new DateTimeOffset(2026, 9, 12, 13, 30, 0, TimeSpan.Zero), next);
    }

    [TestMethod]
    public void PeriodCase_GetNextOccurrence_AfterWindow_MovesToNextWindow()
    {
        var pattern = RecurrencePattern.Parse("*90sec between[9:30am, 2:30pm] tz:America/New_York");
        var after = new DateTimeOffset(2026, 9, 12, 19, 0, 0, TimeSpan.Zero);

        var next = pattern.GetNextOccurrence(after);

        Assert.AreEqual(new DateTimeOffset(2026, 9, 13, 13, 30, 0, TimeSpan.Zero), next);
    }

    [TestMethod]
    public void PeriodCase_InRange2()
    {
        var pattern = RecurrencePattern.Parse("*1hr b[2:30pm, 9:30am] tz:America/New_York");
        Assert.IsTrue(pattern.Period.Equals(TimeSpan.FromHours(1)));
        Assert.AreEqual(new(14, 30), pattern.Between!.Value.Start);
        Assert.AreEqual(new(9, 30), pattern.Between!.Value.End);
    }

    [TestMethod]
    public void PeriodCase_GetNextOccurrence_OvernightWindow_StaysWithinWindow()
    {
        var pattern = RecurrencePattern.Parse("*1hr b[2:30pm, 9:30am] tz:America/New_York");
        var after = new DateTimeOffset(2026, 9, 12, 23, 0, 0, TimeSpan.Zero);

        var next = pattern.GetNextOccurrence(after);

        Assert.AreEqual(new DateTimeOffset(2026, 9, 13, 0, 0, 0, TimeSpan.Zero), next);
    }

    [TestMethod]
    public void InvalidCase()
    {
        try
        {
            RecurrencePattern.Parse("m[1] d[mon]");
            Assert.Fail("Expected a FormatException for mixed recurrence selectors.");
        }
        catch (FormatException)
        {
        }
    }
}
