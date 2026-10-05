using ErpSystem.Core.Entities.HR.CompanySchedule;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Core.Services.HR;

/// <summary>Who a business closure covers: the whole company, one site, or one organisation unit.</summary>
public enum ClosureScopeKind
{
    Company = 1,
    Site = 2,
    Unit = 3,
}

/// <summary>A closure's scope. <see cref="TargetId"/> is the site or unit; null for the whole company.</summary>
public readonly record struct ClosureScope(ClosureScopeKind Kind, Guid? TargetId);

/// <summary>One occurrence of a closure: its first and last day, inclusive.</summary>
public readonly record struct ClosureOccurrence(DateOnly Start, DateOnly End);

/// <summary>
/// The rules a business closure lives by (company-schedule final closure, lane 1: D-1, C-38, C-39).
/// Pure: no database. The service, the closure calendar and the commitment sources all read
/// closures through these, so "who does it cover" and "which days" have one answer.
/// </summary>
/// <remarks>
/// <para><b>The type drives the scope (D-1).</b> A full closure shuts the whole company; a site
/// closure one site; an organisation-unit closure one unit and everything beneath it. A partial
/// closure covers exactly one of the three, and its day still counts as a working day. Before this,
/// the type was a label beside an independent "whole company" switch, and nothing read it.</para>
///
/// <para><b>"Partial" takes ONE scope, not a site and a unit together.</b> The audience resolver
/// unions its rules, so "this unit at that site" could not be expressed — a pair would quietly mean
/// "the site, and also the unit, wherever its staff sit".</para>
///
/// <para><b>A closure that recurs every year (C-38)</b> falls on the same month and day in every
/// year from its first, for the same number of days. One that starts on 29 February falls on
/// 28 February in other years. It must be shorter than a year, or its occurrences would run into
/// each other.</para>
/// </remarks>
public static class BusinessClosureRules
{
    /// <summary>The closure's scope, read from its type.</summary>
    /// <remarks>
    /// For a partial closure the switch and pickers say which of the three it is; for every other
    /// type the type alone decides, whatever the stored flags say.
    /// </remarks>
    public static ClosureScope ScopeOf(BusinessClosure closure) => closure.Type switch
    {
        ClosureType.StationClosure => new ClosureScope(ClosureScopeKind.Site, closure.LocationId),
        ClosureType.DepartmentClosure => new ClosureScope(ClosureScopeKind.Unit, closure.OrganizationUnitId),
        ClosureType.PartialClosure when !closure.AffectsAllStations && closure.LocationId is { } site
            => new ClosureScope(ClosureScopeKind.Site, site),
        ClosureType.PartialClosure when !closure.AffectsAllStations && closure.OrganizationUnitId is { } unit
            => new ClosureScope(ClosureScopeKind.Unit, unit),
        _ => new ClosureScope(ClosureScopeKind.Company, null),
    };

    /// <summary>The scope as an audience rule, for the HR audience resolver.</summary>
    /// <remarks>
    /// ⚠ A <see cref="HrAudienceTargetType.Location"/> rule matches employees assigned to that
    /// exact location, not to locations beneath it: a closure of a region reaches nobody.
    /// </remarks>
    public static HrAudienceRule AudienceRuleOf(ClosureScope scope) => scope.Kind switch
    {
        ClosureScopeKind.Site => new HrAudienceRule(HrAudienceTargetType.Location, scope.TargetId, false),
        ClosureScopeKind.Unit => new HrAudienceRule(HrAudienceTargetType.OrganizationUnit, scope.TargetId, false),
        _ => new HrAudienceRule(HrAudienceTargetType.AllEmployees, null, false),
    };

    /// <summary>Whether the closure's days are days off (anything but a partial closure).</summary>
    public static bool IsNonWorking(BusinessClosure closure) => closure.Type != ClosureType.PartialClosure;

    /// <summary>
    /// Checks a closure's type against its scope and dates, and sets the two flags the type decides.
    /// Answers the sentence to refuse with, or null when the closure is consistent.
    /// </summary>
    /// <remarks>
    /// <para>Refuses a real contradiction — a site on a full closure, a site closure with no site —
    /// rather than guessing which half the person meant.</para>
    ///
    /// <para>⚠ <b>Normalises the two derived flags instead of refusing them.</b> Whether a closure is
    /// company-wide (for every type but partial) and whether its day counts as worked (partial: yes;
    /// every other type: no) follow from the type, so a form that sends the other value is corrected,
    /// not refused. Refusing would make people set a field that has only one right answer.</para>
    /// </remarks>
    public static string? ValidateAndNormalise(BusinessClosure closure)
    {
        if (closure.DepartmentId is not null)
            return "Closures are scoped by organisation unit now, not by department. Choose the organisation unit instead.";

        var start = DateOnly.FromDateTime(closure.StartDate);
        var end = DateOnly.FromDateTime(closure.EndDate);
        if (end < start)
            return "The closure ends before it starts. Set the last day on or after the first.";

        if (closure.RecursAnnually && end.DayNumber - start.DayNumber >= 365)
            return "A closure that recurs every year has to be shorter than a year.";

        var hasSite = closure.LocationId is not null;
        var hasUnit = closure.OrganizationUnitId is not null;

        switch (closure.Type)
        {
            case ClosureType.FullClosure:
                if (hasSite || hasUnit)
                    return "A full closure shuts the whole company, so it takes no site or organisation unit. "
                         + "Choose a site closure or an organisation-unit closure to close part of it.";
                closure.AffectsAllStations = true;
                closure.CountsAsWorkingDay = false;
                return null;

            case ClosureType.StationClosure:
                if (!hasSite)
                    return "A site closure closes one site. Choose the site.";
                if (hasUnit)
                    return "A site closure closes one site, so it takes no organisation unit. "
                         + "Choose an organisation-unit closure to close a unit.";
                closure.AffectsAllStations = false;
                closure.CountsAsWorkingDay = false;
                return null;

            case ClosureType.DepartmentClosure:
                if (!hasUnit)
                    return "An organisation-unit closure closes one unit and everything beneath it. Choose the unit.";
                if (hasSite)
                    return "An organisation-unit closure closes a unit wherever its staff sit, so it takes no site. "
                         + "Choose a site closure to close a site.";
                closure.AffectsAllStations = false;
                closure.CountsAsWorkingDay = false;
                return null;

            case ClosureType.PartialClosure:
                var scopes = (closure.AffectsAllStations ? 1 : 0) + (hasSite ? 1 : 0) + (hasUnit ? 1 : 0);
                if (scopes != 1)
                    return "A partial closure covers one scope: the whole company, one site or one organisation unit. "
                         + "Choose exactly one.";
                closure.CountsAsWorkingDay = true;
                return null;

            default:
                return "Choose the kind of closure: full, partial, site or organisation unit.";
        }
    }

    /// <summary>The closure's own first and last day.</summary>
    public static ClosureOccurrence FirstOccurrence(BusinessClosure closure)
        => new(DateOnly.FromDateTime(closure.StartDate), DateOnly.FromDateTime(closure.EndDate));

    /// <summary>
    /// Every occurrence of the closure that touches <paramref name="from"/>..<paramref name="to"/>,
    /// in date order: the closure itself, or for a recurring one each yearly repeat from its first year.
    /// </summary>
    public static IEnumerable<ClosureOccurrence> OccurrencesIn(BusinessClosure closure, DateOnly from, DateOnly to)
    {
        if (to < from) yield break;

        var first = FirstOccurrence(closure);
        if (!closure.RecursAnnually)
        {
            if (first.Start <= to && first.End >= from) yield return first;
            yield break;
        }

        var span = first.End.DayNumber - first.Start.DayNumber;
        // A repeat that started the year before can still be running on the first day asked about.
        for (var year = Math.Max(first.Start.Year, from.Year - 1); year <= to.Year; year++)
        {
            var day = Math.Min(first.Start.Day, DateTime.DaysInMonth(year, first.Start.Month));
            var start = new DateOnly(year, first.Start.Month, day);
            var end = start.AddDays(span);
            if (start <= to && end >= from) yield return new ClosureOccurrence(start, end);
        }
    }

    /// <summary>Whether the closure covers <paramref name="date"/> in any occurrence.</summary>
    public static bool Covers(BusinessClosure closure, DateOnly date) => OccurrencesIn(closure, date, date).Any();

    /// <summary>Every date the closure covers in <paramref name="from"/>..<paramref name="to"/>.</summary>
    public static IEnumerable<DateOnly> DatesIn(BusinessClosure closure, DateOnly from, DateOnly to)
    {
        foreach (var occurrence in OccurrencesIn(closure, from, to))
        {
            var start = occurrence.Start < from ? from : occurrence.Start;
            var end = occurrence.End > to ? to : occurrence.End;
            for (var day = start; day <= end; day = day.AddDays(1))
                yield return day;
        }
    }

    /// <summary>Whether two closures share a day in any of their occurrences (C-39).</summary>
    public static bool Overlap(BusinessClosure a, BusinessClosure b)
    {
        var aFirst = FirstOccurrence(a);
        var bFirst = FirstOccurrence(b);

        if (!a.RecursAnnually && !b.RecursAnnually)
            return aFirst.Start <= bFirst.End && bFirst.Start <= aFirst.End;
        if (!b.RecursAnnually)
            return OccurrencesIn(a, bFirst.Start, bFirst.End).Any();
        if (!a.RecursAnnually)
            return OccurrencesIn(b, aFirst.Start, aFirst.End).Any();

        // Both repeat yearly and each is shorter than a year, so three years from the later first
        // year hold every way their repeats can line up.
        var year = Math.Max(aFirst.Start.Year, bFirst.Start.Year);
        var from = new DateOnly(year, 1, 1);
        var to = new DateOnly(year + 2, 12, 31);
        var aRepeats = OccurrencesIn(a, from, to).ToList();
        return OccurrencesIn(b, from, to).Any(br => aRepeats.Any(ar => ar.Start <= br.End && br.Start <= ar.End));
    }

    /// <summary>
    /// Narrows a closure query to rows that may have an occurrence in <paramref name="from"/>..<paramref name="to"/>:
    /// overlapping ones, and recurring ones that started by then. Finish with <see cref="OccurrencesIn"/>.
    /// </summary>
    public static IQueryable<BusinessClosure> Candidates(
        IQueryable<BusinessClosure> closures, Guid tenantId, DateOnly from, DateOnly to)
    {
        var fromStart = from.ToDateTime(TimeOnly.MinValue);
        var toExclusive = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return closures.Where(c => c.TenantId == tenantId && !c.IsDeleted
                                 && c.StartDate < toExclusive
                                 && (c.EndDate >= fromStart || c.RecursAnnually));
    }

    /// <summary>"24 Dec 2026" or "24 Dec – 2 Jan 2027", for sentences.</summary>
    public static string Describe(ClosureOccurrence occurrence)
    {
        static string Day(DateOnly d) => d.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        return occurrence.Start == occurrence.End
            ? Day(occurrence.Start)
            : $"{Day(occurrence.Start)} – {Day(occurrence.End)}";
    }
}
