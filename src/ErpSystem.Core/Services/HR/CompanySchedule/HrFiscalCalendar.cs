using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.EntityFrameworkCore;
using FinanceYear = ErpSystem.Core.DTOs.Finance.FiscalYearDto;

namespace ErpSystem.Core.Services.HR.CompanySchedule;

/// <summary>
/// The fiscal calendar as HR reads it (company-schedule final closure lane 4b, D-6) — Finance's, read-only and in-process.
/// </summary>
/// <remarks>
/// <para><b>Why in-process.</b> Finance's fiscal routes need <c>Finance.Read</c> (a global convention puts it on every GET of
/// that controller), which HR's people do not hold. HR's budget actuals already read Finance's periods this way
/// (<c>HrFinanceActualsService</c>); this is the same door, and Finance's service applies the tenant from the token.</para>
///
/// <para><b>A year's status (the user's ruling).</b> Finance closes a year per accounting book — a
/// <see cref="YearEndBookCloseCycle"/> — and never marks the year itself closed; so the year's own status is shown beside
/// each book whose latest close is not reopened.</para>
///
/// <para><b>A year Finance has not opened</b> continues Finance's sequence (the user's ruling, 2026-10-06), by Finance's own
/// rule for its next year — numbered one higher, starting the day after the last ends
/// (<c>FiscalPeriodService.CreateFiscalYearCoreAsync</c> refuses anything else) — each taken as twelve months; before
/// Finance's first year, the same backwards. So a requisition or a budget can never carry a year label that Finance gives
/// to other dates — which the policy month did whenever it disagreed with Finance's start, or Finance labelled a year by
/// the calendar year it ends in. Finance years end at 23:59:59: dates are compared as days.</para>
///
/// <para><b>The fallback.</b> Only while Finance has no fiscal year at all does the policy's "Fiscal year starts" month
/// answer, as <see cref="HrFiscalYear"/> always did — so HR works on a tenant with no Finance calendar.</para>
/// </remarks>
public sealed class HrFiscalCalendar : IHrFiscalCalendar
{
    private const string FinanceSource = "Finance";
    private const string ProjectedSource = "Projected";
    private const string FallbackSource = "Fallback";

    private readonly IFiscalPeriodService _finance;
    private readonly ICompanyHrPolicyProvider _policy;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;

    public HrFiscalCalendar(
        IFiscalPeriodService finance,
        ICompanyHrPolicyProvider policy,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser)
    {
        _finance = finance;
        _policy = policy;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<HrFiscalCalendarDto> GetCalendarAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var years = (await _finance.GetFiscalYearsAsync(cancellationToken)).OrderBy(y => y.StartDate).ToList();
        var periods = await _finance.GetFiscalPeriodsAsync(null, null, cancellationToken);
        var yearIds = years.Select(y => y.Id).ToList();

        // Each book's latest close of each year; a reopened one leaves the book open for that year.
        var cycles = yearIds.Count == 0
            ? new List<YearEndBookCloseCycle>()
            : await _unitOfWork.Repository<YearEndBookCloseCycle>().GetQueryable().AsNoTracking()
                .Include(c => c.AccountingBook)
                .Where(c => c.TenantId == tenantId && yearIds.Contains(c.FiscalYearId))
                .ToListAsync(cancellationToken);
        var latest = cycles
            .GroupBy(c => (c.FiscalYearId, c.AccountingBookId))
            .Select(g => g.OrderByDescending(c => c.CycleNumber).First())
            .Where(c => !string.Equals(c.Status, "Reopened", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var last = years.LastOrDefault();
        return new HrFiscalCalendarDto
        {
            NextYear = last is null ? null : Projected(last.EndDate.Date.AddDays(1), last.Year + 1, 0),
            FallbackStartMonth = (await _policy.GetAsync(cancellationToken)).FiscalYearStartMonth,
            Years = years.Select(y => new HrFiscalCalendarYearDto
            {
                Id = y.Id,
                Name = y.FiscalYearName,
                Code = y.FiscalYearCode,
                Year = y.Year,
                StartDate = y.StartDate.Date,
                EndDate = y.EndDate.Date,
                Status = y.Status,
                IsLocked = y.IsLocked,
                Books = latest.Where(c => c.FiscalYearId == y.Id)
                    .OrderBy(c => c.AccountingBookCode)
                    .Select(c => new HrFiscalBookCloseDto
                    {
                        BookCode = c.AccountingBookCode,
                        BookName = c.AccountingBook?.Name,
                        Status = c.Status,
                        ClosedAtUtc = c.ClosedAtUtc,
                    }).ToList(),
                Periods = periods.Where(p => p.FiscalYearId == y.Id)
                    .OrderBy(p => p.PeriodNumber)
                    .Select(p => new HrFiscalCalendarPeriodDto
                    {
                        Number = p.PeriodNumber,
                        Name = p.PeriodName,
                        StartDate = p.StartDate.Date,
                        EndDate = p.EndDate.Date,
                        Status = p.PeriodStatus,
                    }).ToList(),
            }).ToList(),
        };
    }

    /// <inheritdoc />
    public async Task<HrFiscalYearAnswerDto> YearForDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var day = date.ToDateTime(TimeOnly.MinValue);
        var years = (await _finance.GetFiscalYearsAsync(cancellationToken)).OrderBy(y => y.StartDate).ToList();
        var covering = years.LastOrDefault(y => y.StartDate.Date <= day && y.EndDate.Date >= day);
        if (covering is not null) return FromFinance(covering);

        // ⚠ Not at DateTime's first or last year: a projected year would run past its range.
        if (years.Count > 0 && day.Year is > 1 and < 9999)
        {
            // Forward from the latest Finance year ending before the date; else back from the first.
            var before = years.LastOrDefault(y => y.EndDate.Date < day);
            if (before is not null)
            {
                var origin = before.EndDate.Date.AddDays(1);
                return Projected(origin, before.Year + 1, YearsFrom(origin, day));
            }
            var first = years[0];
            return Projected(first.StartDate.Date, first.Year, YearsFrom(first.StartDate.Date, day));
        }

        var settings = await _policy.GetAsync(cancellationToken);
        return Fallback(HrFiscalYear.For(date, settings), settings.FiscalYearStartMonth);
    }

    /// <inheritdoc />
    public async Task<HrFiscalYearAnswerDto> PeriodForYearAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        var years = await _finance.GetFiscalYearsAsync(cancellationToken);
        var numbered = years.FirstOrDefault(y => y.Year == fiscalYear);
        if (numbered is not null) return FromFinance(numbered);

        if (years.Count > 0 && fiscalYear is > 1 and < 9998)
        {
            // Forward from the highest Finance year numbered below it; else back from the lowest.
            var below = years.Where(y => y.Year < fiscalYear).OrderByDescending(y => y.Year).FirstOrDefault();
            if (below is not null)
                return Projected(below.EndDate.Date.AddDays(1), below.Year + 1, fiscalYear - below.Year - 1);
            var lowest = years.OrderBy(y => y.Year).First();
            return Projected(lowest.StartDate.Date, lowest.Year, fiscalYear - lowest.Year);
        }

        return Fallback(fiscalYear, (await _policy.GetAsync(cancellationToken)).FiscalYearStartMonth);
    }

    /// <summary>Whole years from <paramref name="origin"/> to the year holding <paramref name="day"/> (negative before it).</summary>
    private static int YearsFrom(DateTime origin, DateTime day)
    {
        var steps = day.Year - origin.Year;
        if (origin.AddYears(steps) > day) steps--;
        return steps;
    }

    /// <summary>
    /// The year <paramref name="steps"/> years from a year starting on <paramref name="origin"/> and labelled
    /// <paramref name="originLabel"/>: twelve months each, measured from the origin every time (so 29 February stays put
    /// in leap years and the years tile without gaps).
    /// </summary>
    private static HrFiscalYearAnswerDto Projected(DateTime origin, int originLabel, int steps) => new()
    {
        FiscalYear = originLabel + steps,
        StartDate = origin.AddYears(steps),
        EndDate = origin.AddYears(steps + 1).AddDays(-1),
        Source = ProjectedSource,
    };

    private static HrFiscalYearAnswerDto FromFinance(FinanceYear y) => new()
    {
        FiscalYear = y.Year,
        StartDate = y.StartDate.Date,
        EndDate = y.EndDate.Date,
        Source = FinanceSource,
        Name = y.FiscalYearName,
    };

    /// <summary>
    /// The fallback's year: labelled by the calendar year it starts in (<see cref="HrFiscalYear"/>), from the first of the
    /// start month to the day before it a year later.
    /// </summary>
    private static HrFiscalYearAnswerDto Fallback(int fiscalYear, int startMonth)
    {
        var month = startMonth is >= 1 and <= 12 ? startMonth : 1;
        var start = new DateTime(fiscalYear, month, 1);
        return new HrFiscalYearAnswerDto
        {
            FiscalYear = fiscalYear,
            StartDate = start,
            EndDate = start.AddYears(1).AddDays(-1),
            Source = FallbackSource,
        };
    }
}
