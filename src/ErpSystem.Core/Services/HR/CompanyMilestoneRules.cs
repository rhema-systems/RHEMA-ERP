using ErpSystem.Core.Entities.HR.CompanySchedule;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// A company milestone's dates (company-schedule final closure lane 4a, C-40) — pure, so the service, the screens' wording
/// and the harness read the same answers.
/// </summary>
/// <remarks>
/// <para>⚠ "Repeats every year" was stored and read by nothing: the upcoming read compared the milestone's own date, so a
/// company's founding anniversary was "upcoming" in its first year only.</para>
///
/// <para>A milestone that repeats falls on the same month and day in every year from its own — the closures' yearly rule
/// (<see cref="BusinessClosureRules.OccurrencesIn"/>, C-38): one on 29 February falls on 28 February in other years. A
/// one-off falls once, on its date.</para>
/// </remarks>
public static class CompanyMilestoneRules
{
    /// <summary>The widest range a dated read answers: a repeating milestone answers one row per year in it.</summary>
    public const int MaxRangeYears = 5;

    /// <summary>Its occurrence in <paramref name="year"/>, or null — a one-off in another year, or a year before its own.</summary>
    public static DateOnly? OccurrenceIn(CompanyMilestone m, int year)
    {
        var first = DateOnly.FromDateTime(m.MilestoneDate);
        if (!m.IsRecurringAnnually) return first.Year == year ? first : null;
        if (year < first.Year) return null;
        return new DateOnly(year, first.Month, Math.Min(first.Day, DateTime.DaysInMonth(year, first.Month)));
    }

    /// <summary>Every occurrence in <paramref name="from"/>..<paramref name="to"/>, in date order.</summary>
    public static IEnumerable<DateOnly> OccurrencesIn(CompanyMilestone m, DateOnly from, DateOnly to)
    {
        if (to < from) yield break;
        for (var year = from.Year; year <= to.Year; year++)
            if (OccurrenceIn(m, year) is { } d && d >= from && d <= to)
                yield return d;
    }

    /// <summary>The next occurrence on or after <paramref name="today"/>; null for a one-off already past.</summary>
    public static DateOnly? NextOccurrence(CompanyMilestone m, DateOnly today)
    {
        var first = DateOnly.FromDateTime(m.MilestoneDate);
        if (first >= today) return first;
        if (!m.IsRecurringAnnually) return null;
        return OccurrenceIn(m, today.Year) is { } thisYear && thisYear >= today ? thisYear : OccurrenceIn(m, today.Year + 1);
    }

    /// <summary>Whole years from its own date to <paramref name="occurrence"/> — "the 10th anniversary"; 0 on the first.</summary>
    public static int YearsSince(CompanyMilestone m, DateOnly occurrence) => occurrence.Year - m.MilestoneDate.Year;

    /// <summary>A milestone needs a date — <c>[Required]</c> on a non-nullable date does nothing, and year 1 was stored.</summary>
    public static string? RefuseDate(DateTime date) =>
        date == default ? "Say when the milestone is." : null;
}
