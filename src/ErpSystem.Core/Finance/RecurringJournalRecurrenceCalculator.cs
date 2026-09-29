using System.Text.Json;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.Finance;

public sealed record RecurrenceRule(
    int[]? Weekdays = null,
    int[]? DaysOfMonth = null,
    int? NthWeek = null,
    int? NthWeekday = null,
    bool LastCalendarDay = false,
    bool LastBusinessDay = false,
    int[]? SelectedMonths = null);

public interface IBusinessCalendarProvider
{
    Task<bool> IsBusinessDayAsync(Guid tenantId, DateOnly date, CancellationToken cancellationToken = default);
}

public static class RecurringJournalRecurrenceCalculator
{
    public static RecurrenceRule ParseRule(string? json) =>
        string.IsNullOrWhiteSpace(json) ? new() : JsonSerializer.Deserialize<RecurrenceRule>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new();

    public static DateOnly? NextScheduledDate(RecurringJournalTemplate template, DateOnly after)
    {
        var rule = ParseRule(template.RecurrenceRuleJson);
        var candidate = after.AddDays(1);
        var hardStop = candidate.AddYears(10);
        while (candidate <= hardStop)
        {
            if (template.EndDate is { } end && candidate > end) return null;
            if (template.MaximumOccurrences is { } max && template.GeneratedOccurrenceCount >= max) return null;
            if (candidate >= template.EffectiveFrom && Matches(template, rule, candidate)) return candidate;
            candidate = candidate.AddDays(1);
        }
        throw new InvalidOperationException("Recurrence rule produced no date within ten years.");
    }

    public static async Task<DateOnly> AdjustBusinessDayAsync(Guid tenantId, DateOnly scheduled,
        BusinessDayConvention convention, IBusinessCalendarProvider calendar, CancellationToken cancellationToken = default)
    {
        if (convention == BusinessDayConvention.NoAdjustment || await calendar.IsBusinessDayAsync(tenantId, scheduled, cancellationToken)) return scheduled;
        var step = convention == BusinessDayConvention.NextBusinessDay ? 1 : -1;
        var adjusted = scheduled;
        do adjusted = adjusted.AddDays(step);
        while (!await calendar.IsBusinessDayAsync(tenantId, adjusted, cancellationToken));
        return adjusted;
    }

    private static bool Matches(RecurringJournalTemplate t, RecurrenceRule r, DateOnly d)
    {
        if (r.SelectedMonths is { Length: > 0 } && !r.SelectedMonths.Contains(d.Month)) return false;
        var interval = Math.Max(1, t.Interval);
        var days = d.DayNumber - t.EffectiveFrom.DayNumber;
        return t.Frequency switch
        {
            RecurrenceFrequency.Daily => days % interval == 0,
            RecurrenceFrequency.Weekly => days >= 0 && (days / 7) % interval == 0 && (r.Weekdays?.Contains((int)d.DayOfWeek) ?? d.DayOfWeek == t.EffectiveFrom.DayOfWeek),
            RecurrenceFrequency.SemiMonthly => MonthDistance(t.EffectiveFrom, d) % interval == 0 &&
                (r.DaysOfMonth ?? [1, 15]).Select(x => Math.Min(x, DateTime.DaysInMonth(d.Year, d.Month))).Contains(d.Day),
            RecurrenceFrequency.Monthly => MonthDistance(t.EffectiveFrom, d) % interval == 0 && MatchesMonthly(r, t.EffectiveFrom, d),
            RecurrenceFrequency.Quarterly => MonthDistance(t.EffectiveFrom, d) % (3 * interval) == 0 && MatchesMonthly(r, t.EffectiveFrom, d),
            RecurrenceFrequency.Annually => d.Year >= t.EffectiveFrom.Year && (d.Year - t.EffectiveFrom.Year) % interval == 0 && d.Month == t.EffectiveFrom.Month && MatchesMonthly(r, t.EffectiveFrom, d),
            RecurrenceFrequency.Custom => MatchesCustom(r, t.EffectiveFrom, d, interval),
            _ => false
        };
    }

    private static int MonthDistance(DateOnly from, DateOnly to) => (to.Year - from.Year) * 12 + to.Month - from.Month;
    private static bool MatchesMonthly(RecurrenceRule r, DateOnly start, DateOnly d) =>
        r.LastCalendarDay ? d.Day == DateTime.DaysInMonth(d.Year, d.Month) :
        r.LastBusinessDay ? IsLastWeekdayOfMonth(d) :
        r.DaysOfMonth is { Length: > 0 } ? r.DaysOfMonth.Select(x => Math.Min(x, DateTime.DaysInMonth(d.Year, d.Month))).Contains(d.Day) :
        d.Day == Math.Min(start.Day, DateTime.DaysInMonth(d.Year, d.Month));

    private static bool MatchesCustom(RecurrenceRule r, DateOnly start, DateOnly d, int interval)
    {
        var monthAligned = MonthDistance(start, d) % interval == 0;
        if (r.LastCalendarDay && monthAligned && d.Day == DateTime.DaysInMonth(d.Year, d.Month)) return true;
        if (r.LastBusinessDay && monthAligned && IsLastWeekdayOfMonth(d)) return true;
        if (monthAligned && r.DaysOfMonth?.Select(x => Math.Min(x, DateTime.DaysInMonth(d.Year, d.Month))).Contains(d.Day) == true) return true;
        if (r.Weekdays?.Contains((int)d.DayOfWeek) == true && r.NthWeek is null &&
            ((d.DayNumber - start.DayNumber) / 7) % interval == 0) return true;
        if (r.NthWeekday is { } weekday && r.NthWeek is { } nth && (int)d.DayOfWeek == weekday)
        {
            if (!monthAligned) return false;
            if (nth == -1) return d.AddDays(7).Month != d.Month;
            return ((d.Day - 1) / 7) + 1 == nth;
        }
        return false;
    }

    private static bool IsLastWeekdayOfMonth(DateOnly date)
    {
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;
        var next = date.AddDays(1);
        while (next.Month == date.Month && (next.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday))
            next = next.AddDays(1);
        return next.Month != date.Month;
    }
}
