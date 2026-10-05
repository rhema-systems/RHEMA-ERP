using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// A recurring event's dates (company-schedule final closure, lane 2f-1; D-2, D-12) — pure, so the service, the
/// screens' wording and the harness read the same answers.
/// </summary>
/// <remarks>
/// <para><b>A light series (D-12):</b> the occurrences ARE the series — full events tied by
/// <c>RecurrenceSeriesId</c> and numbered by <c>OccurrenceNumber</c>, with no series table.</para>
///
/// <para><b>Every date is counted from the first</b>, never from the one before: a monthly series begun on the 31st
/// falls on the last day of a shorter month and on the 31st again after it, and a yearly one begun on 29 February
/// falls on the 28th in a common year. .NET's <c>AddMonths</c>/<c>AddYears</c> clamp exactly so.</para>
///
/// <para>An occurrence on a public holiday or a company-wide closure is generated and flagged, never skipped
/// (D-12): moving it is HR's decision.</para>
/// </remarks>
public static class CompanyEventSeries
{
    /// <summary>The most occurrences one series holds (D-2); a longer one is extended later.</summary>
    public const int MaxOccurrences = 52;

    /// <summary>The start date of the occurrence at <paramref name="index"/> (0 is the first).</summary>
    public static DateTime DateAt(RecurrencePattern pattern, DateTime first, int index)
    {
        var day = first.Date;
        return pattern switch
        {
            RecurrencePattern.Daily => day.AddDays(index),
            RecurrencePattern.Weekdays => AddWeekdays(day, index),
            RecurrencePattern.Weekly => day.AddDays(7 * index),
            RecurrencePattern.BiWeekly => day.AddDays(14 * index),
            RecurrencePattern.Monthly => day.AddMonths(index),
            RecurrencePattern.Quarterly => day.AddMonths(3 * index),
            RecurrencePattern.Annually => day.AddYears(index),
            _ => throw new ArgumentOutOfRangeException(nameof(pattern), pattern, "Not a recurrence pattern."),
        };
    }

    /// <summary>The weekday <paramref name="count"/> weekdays after <paramref name="day"/> (itself a weekday).</summary>
    private static DateTime AddWeekdays(DateTime day, int count)
    {
        var date = day.AddDays(7 * (count / 5));
        for (var left = count % 5; left > 0;)
        {
            date = date.AddDays(1);
            if (!IsWeekend(date)) left--;
        }
        return date;
    }

    private static bool IsWeekend(DateTime d) => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    /// <summary>
    /// How many occurrences start on or before <paramref name="until"/> — at most <see cref="MaxOccurrences"/> + 1,
    /// so a caller can tell "too many" without walking a hundred years.
    /// </summary>
    public static int CountUntil(RecurrencePattern pattern, DateTime first, DateTime until)
    {
        var n = 0;
        while (n <= MaxOccurrences && DateAt(pattern, first, n) <= until.Date) n++;
        return n;
    }

    /// <summary>The smallest gap between two occurrences, in days — what a multi-day event must fit inside.</summary>
    private static int ShortestGapDays(RecurrencePattern pattern) => pattern switch
    {
        RecurrencePattern.Daily or RecurrencePattern.Weekdays => 1,
        RecurrencePattern.Weekly => 7,
        RecurrencePattern.BiWeekly => 14,
        RecurrencePattern.Monthly => 28,
        RecurrencePattern.Quarterly => 89,
        _ => 365,
    };

    /// <summary>"weekly", "every weekday" — for sentences.</summary>
    public static string Describe(RecurrencePattern pattern) => pattern switch
    {
        RecurrencePattern.Daily => "every day",
        RecurrencePattern.Weekdays => "every weekday",
        RecurrencePattern.Weekly => "every week",
        RecurrencePattern.BiWeekly => "every two weeks",
        RecurrencePattern.Monthly => "every month",
        RecurrencePattern.Quarterly => "every quarter",
        RecurrencePattern.Annually => "every year",
        _ => pattern.ToString(),
    };

    /// <summary>
    /// Checks a series about to be made, and answers how many occurrences it makes — or the sentence to refuse with.
    /// </summary>
    /// <param name="durationDays">How many days each occurrence spans beyond its first (0 for a one-day event).</param>
    /// <param name="already">Occurrences the series has already (extending it); 0 when making it.</param>
    public static (int Count, string? Refusal) Plan(
        RecurrencePattern? pattern, int? count, DateTime? until, DateTime first, int durationDays, int already = 0)
    {
        if (pattern is not { } p || !Enum.IsDefined(p))
            return (0, "Choose how it repeats: every day, every weekday, every week, every two weeks, every month, every quarter or every year.");
        if (count is not null && until is not null)
            return (0, "Give either how many times it repeats or the date it repeats until — not both.");
        if (count is null && until is null)
            return (0, "Give how many times it repeats, or the date it repeats until.");
        if (p == RecurrencePattern.Weekdays && IsWeekend(first))
            return (0, "A weekday series starts on a weekday. Choose a Monday to Friday for its first date.");
        if (durationDays >= ShortestGapDays(p))
            return (0, $"Each occurrence lasts {durationDays + 1} days, which is longer than the gap between them ({Describe(p)}). "
                       + "Shorten the event or choose a longer gap.");

        int n;
        if (count is { } c)
        {
            n = already == 0 ? c : already + c;
            if (already == 0 && c < 2)
                return (0, "A series repeats at least twice. For a single meeting, switch Repeats off.");
            if (already > 0 && c < 1)
                return (0, "Add at least one occurrence.");
        }
        else
        {
            if (until!.Value.Date <= DateAt(p, first, Math.Max(0, already - 1)).Date)
                return (0, already == 0
                    ? "The date it repeats until must come after its first date."
                    : "The date it repeats until must come after the series' last occurrence.");
            n = CountUntil(p, first, until.Value);
            if (already == 0 && n < 2)
                return (0, $"Until that date it would happen only once ({Describe(p)}). Choose a later date.");
            if (already > 0 && n <= already)
                return (0, $"No further occurrence falls on or before that date ({Describe(p)}). Choose a later date.");
        }

        if (n > MaxOccurrences)
            return (0, already == 0
                ? $"That makes {(count is null ? "more than 52" : n.ToString())} occurrences; a series holds at most {MaxOccurrences}. "
                  + "Make it shorter — it can be extended later."
                : $"That would take the series to {(count is null ? "more than 52" : n.ToString())} occurrences; a series holds at most {MaxOccurrences}.");
        return (n, null);
    }
}
