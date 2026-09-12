using ScheduleAbstractions;

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
