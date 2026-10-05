using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Counts working days for HR's statutory deadlines — Monday to Friday, less the tenant's public
/// holidays.
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> FR-HR-180 gives an employee five WORKING days to file an appeal and
/// ten WORKING days for it to be decided. Counting those in calendar days would quietly shorten both
/// — five calendar days spanning a weekend is three working days, and over Easter it can be two. On a
/// clock that decides whether someone's appeal was filed in time, that difference is the whole
/// question.</para>
///
/// <para><b>Why the org's calendar and not the employee's schedule.</b> Area 3 has per-employee work
/// schedules and shift rotations, and it would be possible to count against the appellant's own
/// roster. That would be wrong. A statutory deadline is a property of the process, not of the person
/// — if two employees appeal the same decision on the same day, their deadlines must fall on the same
/// date, or the rule means something different depending on who you are. So this counts Monday to
/// Friday against the tenant's default holiday calendar, and nothing employee-specific enters it.</para>
///
/// <para><b>The weekend is hard-coded to Saturday and Sunday</b>, which is TDC's working week. If a
/// tenant ever needs a different one, the place to put it is a tenant policy flag read here, NOT the
/// per-employee schedules — for the reason above.</para>
///
/// <para>Holidays come from the tenant's default <see cref="HolidayCalendar"/>. A tenant with no
/// calendar configured simply gets Monday-to-Friday, which is the safe direction: the deadline lands
/// no later than it should, so nobody's appeal is rejected because a holiday was missed.</para>
/// </remarks>
public interface IHrWorkingDayCalculator
{
    /// <summary>
    /// The date <paramref name="workingDays"/> working days after <paramref name="from"/>.
    /// The starting day is not counted, so "five working days from Monday" is the following Monday.
    /// </summary>
    Task<DateTime> AddWorkingDaysAsync(Guid tenantId, DateTime from, int workingDays, CancellationToken cancellationToken = default);

    /// <summary>
    /// Working days elapsed from <paramref name="from"/> to <paramref name="to"/>, not counting the
    /// starting day. Zero when <paramref name="to"/> is on or before it.
    /// </summary>
    Task<int> CountWorkingDaysAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every date the tenant does not work in <paramref name="from"/>..<paramref name="to"/>, because
    /// a holiday covers it or stands in lieu of one. Weekends are NOT included — a caller that counts
    /// weekends differently (leave types can be configured either way) decides that for itself.
    /// </summary>
    /// <remarks>
    /// Exposed so leave can share this module's one answer to "is this a holiday" instead of keeping
    /// a second, looser one. Leave's own query matched on tenant alone, so it counted holidays from
    /// every calendar including retired ones, and never saw a substitution date (closure plan
    /// L-31/L-32).
    /// </remarks>
    Task<IReadOnlySet<DateOnly>> GetHolidayDatesAsync(
        Guid tenantId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>
    /// The same days as <see cref="GetHolidayDatesAsync"/>, each with the holiday's name, in date
    /// order — for screens that say WHICH holiday (company-schedule final closure, R4-10A.4).
    /// </summary>
    /// <remarks>
    /// The diaries and the clash check used to read every holiday of every calendar, retired and
    /// optional ones included; this keeps them on the one definition leave and the statutory clocks
    /// already use.
    /// </remarks>
    Task<IReadOnlyList<HrHolidayDay>> GetHolidaysAsync(
        Guid tenantId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

/// <summary>A day the tenant does not work because of a holiday, and the holiday's name.</summary>
/// <param name="InLieu">True when the day is the working day given in lieu of a holiday on a weekend.</param>
public sealed record HrHolidayDay(DateOnly Date, string Name, bool InLieu);

public sealed class HrWorkingDayCalculator : IHrWorkingDayCalculator
{
    /// <summary>
    /// A guard against a caller asking for an absurd span and this walking day by day for ever.
    /// Nothing in HR's statutory clocks comes close — the longest is ten working days.
    /// </summary>
    private const int MaxDaysToWalk = 3650;

    private readonly IUnitOfWork _unitOfWork;

    public HrWorkingDayCalculator(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<DateTime> AddWorkingDaysAsync(Guid tenantId, DateTime from, int workingDays, CancellationToken cancellationToken = default)
    {
        if (workingDays <= 0) return from;

        var holidays = await LoadHolidaysAsync(tenantId, cancellationToken);

        var cursor = from.Date;
        var counted = 0;
        var walked = 0;

        while (counted < workingDays && walked < MaxDaysToWalk)
        {
            cursor = cursor.AddDays(1);
            walked++;
            if (IsWorkingDay(cursor, holidays)) counted++;
        }

        // Carry the original time of day: a deadline of "five working days from 14:30 on Monday"
        // expires at 14:30, not at midnight, and truncating would quietly shorten every window.
        return cursor.Add(from.TimeOfDay);
    }

    public async Task<int> CountWorkingDaysAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        if (to.Date <= from.Date) return 0;

        var holidays = await LoadHolidaysAsync(tenantId, cancellationToken);

        var counted = 0;
        var cursor = from.Date;
        var walked = 0;

        while (cursor < to.Date && walked < MaxDaysToWalk)
        {
            cursor = cursor.AddDays(1);
            walked++;
            if (IsWorkingDay(cursor, holidays)) counted++;
        }

        return counted;
    }

    private static bool IsWorkingDay(DateTime day, HashSet<DateOnly> holidays)
        => day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
           && !holidays.Contains(DateOnly.FromDateTime(day));

    /// <remarks>
    /// A holiday can span several days (<c>DateFrom</c>..<c>DateTo</c>), so each one is expanded into
    /// the individual dates it covers. A <c>SubstitutionDate</c> — the working day given in lieu when
    /// a holiday falls on a weekend — is treated as a non-working day too, because that is what it is
    /// for the employee trying to file an appeal.
    /// </remarks>
    public async Task<IReadOnlySet<DateOnly>> GetHolidayDatesAsync(
        Guid tenantId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        if (to < from) return new HashSet<DateOnly>();

        var all = await LoadHolidaysAsync(tenantId, cancellationToken);
        all.RemoveWhere(d => d < from || d > to);
        return all;
    }

    public async Task<IReadOnlyList<HrHolidayDay>> GetHolidaysAsync(
        Guid tenantId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        if (to < from) return [];

        return (await LoadNamedHolidaysAsync(tenantId, cancellationToken))
            .Where(h => h.Date >= from && h.Date <= to)
            .OrderBy(h => h.Date)
            .ToList();
    }

    private async Task<HashSet<DateOnly>> LoadHolidaysAsync(Guid tenantId, CancellationToken cancellationToken)
        => (await LoadNamedHolidaysAsync(tenantId, cancellationToken)).Select(h => h.Date).ToHashSet();

    private async Task<List<HrHolidayDay>> LoadNamedHolidaysAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var calendar = await _unitOfWork.Repository<HolidayCalendar>()
            .GetQueryable()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && c.IsActive)
            .OrderByDescending(c => c.IsDefault)
            .FirstOrDefaultAsync(cancellationToken);

        var days = new List<HrHolidayDay>();
        if (calendar == null) return days;

        var holidays = await _unitOfWork.Repository<PublicHoliday>()
            .GetQueryable()
            .Where(h => h.TenantId == tenantId && !h.IsDeleted && h.IsActive
                     && h.HolidayCalendarId == calendar.Id)
            .ToListAsync(cancellationToken);

        foreach (var holiday in holidays)
        {
            // An OPTIONAL holiday is a day the office is open and an employee may choose to take —
            // so it is a working day, and taking it is leave like any other. Only Mandatory and
            // SubstituteDay close the tenant. Before this, every holiday closed it regardless of
            // what the observance type said, which made the setting decorative (closure plan L-33).
            if (holiday.ObservanceType == HolidayObservanceType.Optional)
                continue;

            for (var day = holiday.DateFrom; day <= holiday.DateTo; day = day.AddDays(1))
                days.Add(new HrHolidayDay(day, holiday.HolidayName, InLieu: false));

            // The working day given in lieu when a holiday falls on a weekend is not worked either.
            if (holiday.SubstitutionDate is DateOnly substitute)
                days.Add(new HrHolidayDay(substitute, holiday.HolidayName, InLieu: true));
        }

        return days;
    }
}
