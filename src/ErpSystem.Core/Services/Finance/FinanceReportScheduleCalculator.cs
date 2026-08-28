using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Services.Finance;

/// <summary>
/// Pure recurrence calculator shared by API and background execution. Ghana is
/// UTC year-round, so schedule wall-clock values can be persisted as UTC without
/// daylight-saving ambiguity.
/// </summary>
public static class FinanceReportScheduleCalculator
{
    public static DateTime FirstOccurrence(
        string frequency,
        DateTime startDate,
        TimeOnly timeOfDay,
        int? dayOfWeek,
        int? dayOfMonth,
        DateTime notBeforeUtc)
    {
        var candidate = AtTime(startDate, timeOfDay);
        candidate = Align(candidate, frequency, dayOfWeek, dayOfMonth);
        while (candidate < notBeforeUtc)
            candidate = NextOccurrence(frequency, candidate, dayOfWeek, dayOfMonth);
        return candidate;
    }

    public static DateTime NextOccurrence(string frequency, DateTime occurrenceUtc, int? dayOfWeek, int? dayOfMonth)
    {
        Validate(frequency, dayOfWeek, dayOfMonth);
        if (frequency.Equals(ReportAutomationValues.Daily, StringComparison.OrdinalIgnoreCase))
            return occurrenceUtc.AddDays(1);
        if (frequency.Equals(ReportAutomationValues.Weekly, StringComparison.OrdinalIgnoreCase))
            return occurrenceUtc.AddDays(7);
        if (frequency.Equals(ReportAutomationValues.Monthly, StringComparison.OrdinalIgnoreCase))
            return InMonth(occurrenceUtc.AddMonths(1), dayOfMonth!.Value);
        if (frequency.Equals(ReportAutomationValues.Quarterly, StringComparison.OrdinalIgnoreCase))
            return InMonth(occurrenceUtc.AddMonths(3), dayOfMonth!.Value);
        return InMonth(occurrenceUtc.AddYears(1), dayOfMonth!.Value);
    }

    public static void Validate(string frequency, int? dayOfWeek, int? dayOfMonth)
    {
        if (!ReportAutomationValues.Frequencies.Contains(frequency))
            throw new InvalidOperationException("Frequency must be Daily, Weekly, Monthly, Quarterly, or Yearly.");
        if (frequency.Equals(ReportAutomationValues.Weekly, StringComparison.OrdinalIgnoreCase) && dayOfWeek is not (>= 0 and <= 6))
            throw new InvalidOperationException("Weekly schedules require a day of week from 0 (Sunday) to 6 (Saturday).");
        if ((frequency.Equals(ReportAutomationValues.Monthly, StringComparison.OrdinalIgnoreCase)
             || frequency.Equals(ReportAutomationValues.Quarterly, StringComparison.OrdinalIgnoreCase)
             || frequency.Equals(ReportAutomationValues.Yearly, StringComparison.OrdinalIgnoreCase))
            && dayOfMonth is not (>= 1 and <= 31))
            throw new InvalidOperationException("Monthly, quarterly, and yearly schedules require a day of month from 1 to 31.");
    }

    private static DateTime Align(DateTime value, string frequency, int? dayOfWeek, int? dayOfMonth)
    {
        Validate(frequency, dayOfWeek, dayOfMonth);
        if (frequency.Equals(ReportAutomationValues.Weekly, StringComparison.OrdinalIgnoreCase))
        {
            var offset = (dayOfWeek!.Value - (int)value.DayOfWeek + 7) % 7;
            return value.AddDays(offset);
        }
        return frequency.Equals(ReportAutomationValues.Daily, StringComparison.OrdinalIgnoreCase)
            ? value
            : InMonth(value, dayOfMonth!.Value);
    }

    private static DateTime AtTime(DateTime date, TimeOnly time) =>
        DateTime.SpecifyKind(date.Date.Add(time.ToTimeSpan()), DateTimeKind.Utc);

    private static DateTime InMonth(DateTime value, int requestedDay)
    {
        // A schedule for the 31st runs on the last valid day of shorter months;
        // it is not silently skipped, which is important for month-end packs.
        var day = Math.Min(requestedDay, DateTime.DaysInMonth(value.Year, value.Month));
        return new DateTime(value.Year, value.Month, day, value.Hour, value.Minute, value.Second, DateTimeKind.Utc);
    }
}
