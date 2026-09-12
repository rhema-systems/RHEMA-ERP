using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Interfaces;
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
}

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
    private async Task<HashSet<DateOnly>> LoadHolidaysAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var calendar = await _unitOfWork.Repository<HolidayCalendar>()
            .GetQueryable()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && c.IsActive)
            .OrderByDescending(c => c.IsDefault)
            .FirstOrDefaultAsync(cancellationToken);

        var dates = new HashSet<DateOnly>();
        if (calendar == null) return dates;

        var holidays = await _unitOfWork.Repository<PublicHoliday>()
            .GetQueryable()
            .Where(h => h.TenantId == tenantId && !h.IsDeleted && h.HolidayCalendarId == calendar.Id)
            .ToListAsync(cancellationToken);

        foreach (var holiday in holidays)
        {
            for (var day = holiday.DateFrom; day <= holiday.DateTo; day = day.AddDays(1))
                dates.Add(day);

            if (holiday.SubstitutionDate is DateOnly substitute)
                dates.Add(substitute);
        }

        return dates;
    }
}
