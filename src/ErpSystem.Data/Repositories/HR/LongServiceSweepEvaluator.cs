using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Awards;
using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

/// <summary>
/// Judges every employee against a long-service ladder, once.
/// </summary>
/// <remarks>
/// <para><b>One calculation, three readers.</b> The sweep preview, the sweep run and the FR-HR-113
/// eligibility report all read this, each projecting the slice it needs. Computing the qualified set
/// here and the report's rows somewhere else would let the two drift, and a report that listed
/// somebody the button then refused to award would be worse than no report at all.</para>
///
/// <para><b>Service is counted in completed years</b> via <c>HrPolicyCalculations.CompletedYears</c>
/// — the single implementation area 14 slice 3b consolidated. The arithmetic matters more here than
/// anywhere else in the module: a milestone is a threshold, and the old calendar-year subtraction
/// would have granted a ten-year award to somebody with nine years and one month.</para>
///
/// <para>⚠ <b>Measured 2026-08-21, and this shapes what a live run can show.</b> Of 5,579 employees,
/// <b>2,103</b> carry a <c>DateEmployed</c> at all, exactly <b>one</b> has ten completed years, and
/// <b>none</b> has fifteen. A live sweep therefore finds at most one person. That is a fact about
/// TDC's employee records, not about this engine, which is why every employee gets a verdict —
/// including <see cref="LongServiceStanding.ServiceUnknown"/> — rather than being dropped from a
/// list whose length would then describe the data and read as describing the staff.</para>
/// </remarks>
public class LongServiceSweepEvaluator : ILongServiceSweepEvaluator
{
    private readonly ApplicationDbContext _context;

    public LongServiceSweepEvaluator(ApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyList<LongServiceVerdict>> EvaluateAsync(
        AwardType awardType, IReadOnlyList<LongServiceMilestone> ladder, Guid tenantId, DateTime asOf)
    {
        var employees = await (
            from e in _context.Set<Employee>()
            where e.TenantId == tenantId && !e.IsDeleted && e.TerminationDate == null
            join d in _context.Set<Department>() on e.DepartmentId equals d.Id into dj
            from d in dj.DefaultIfEmpty()
            select new
            {
                e.Id,
                e.FirstName,
                e.LastName,
                e.EmployeeNumber,
                e.DateEmployed,
                DepartmentName = d == null ? null : d.Name,
            }).ToListAsync();

        // The highest rung each employee has already been granted, so a second run does not grant a
        // second award.
        //
        // ⚠ It has to be the HIGHEST, not the exact pairs. Excluding only (employee, rung) pairs
        // already granted looks equivalent and is not: an employee first swept at twenty-two years is
        // granted the twenty-year rung, and on the next run the fifteen- and ten-year rungs still
        // satisfy "reached, and not yet granted". The sweep then walks *backwards* down the ladder,
        // one award per run, until it reaches the bottom. Measured: the first version of this code
        // did exactly that, and only the two employees past more than one rung showed it.
        //
        // A milestone that passed before the system was granting them is missed, not owed. Awarding
        // it retrospectively three seconds after the twenty-year award would put two certificates on
        // one wall in the wrong order.
        var alreadyGranted = await _context.Set<LongServiceAward>()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.AwardTypeId == awardType.Id)
            .Select(a => new { a.EmployeeId, a.YearsOfService })
            .ToListAsync();

        var highestGranted = alreadyGranted
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(g => g.Key, g => g.Max(a => a.YearsOfService));

        var exempt = awardType.DisqualifyOnDisciplinaryRecord
            ? await LoadDisqualifiedAsync(tenantId, asOf, awardType.DisqualifyingDisciplineMonths)
            : new HashSet<Guid>();

        var rungs = ladder.Where(m => m.IsActive).OrderBy(m => m.Years).ToList();
        var asOfDate = DateOnly.FromDateTime(asOf);
        var verdicts = new List<LongServiceVerdict>(employees.Count);

        foreach (var employee in employees)
        {
            var name = $"{employee.FirstName} {employee.LastName}".Trim();
            var held = highestGranted.TryGetValue(employee.Id, out var h) ? h : (int?)null;

            if (employee.DateEmployed == null)
            {
                verdicts.Add(new LongServiceVerdict
                {
                    EmployeeId = employee.Id,
                    EmployeeName = name,
                    EmployeeNumber = employee.EmployeeNumber,
                    DepartmentName = employee.DepartmentName,
                    Standing = LongServiceStanding.ServiceUnknown,
                    HighestGrantedYears = held,
                    Reason = "No employment date is on record, so this employee's service cannot be measured.",
                });
                continue;
            }

            var years = HrPolicyCalculations.CompletedYears(employee.DateEmployed, asOfDate) ?? 0;

            // The highest rung reached that stands above everything this employee already holds.
            // Sweeping every rung at once would hand somebody who joined twenty years ago four
            // awards in one run; sweeping the ones below would hand them out one per run afterwards.
            var rung = rungs
                .Where(m => years >= m.Years && m.Years > (held ?? 0))
                .OrderByDescending(m => m.Years)
                .FirstOrDefault();

            if (rung == null)
            {
                // Two very different situations, and a report has to tell them apart: somebody who
                // has not served long enough yet, and somebody who already holds everything their
                // service has earned. Collapsing both into "not eligible" would make a thirty-year
                // veteran indistinguishable from a new joiner.
                verdicts.Add(new LongServiceVerdict
                {
                    EmployeeId = employee.Id,
                    EmployeeName = name,
                    EmployeeNumber = employee.EmployeeNumber,
                    DepartmentName = employee.DepartmentName,
                    ServiceStartDate = employee.DateEmployed,
                    YearsOfService = years,
                    HighestGrantedYears = held,
                    MilestoneYears = held,
                    Standing = held.HasValue
                        ? LongServiceStanding.AlreadyGranted
                        : LongServiceStanding.NotYetAtMilestone,
                    Reason = held.HasValue
                        ? $"Already holds the {held}-year award, the highest rung this service has reached."
                        : NextRungMessage(rungs, years),
                });
                continue;
            }

            var isExempt = exempt.Contains(employee.Id);

            verdicts.Add(new LongServiceVerdict
            {
                EmployeeId = employee.Id,
                EmployeeName = name,
                EmployeeNumber = employee.EmployeeNumber,
                DepartmentName = employee.DepartmentName,
                ServiceStartDate = employee.DateEmployed,
                YearsOfService = years,
                MilestoneYears = rung.Years,
                MilestoneId = rung.Id,
                MilestoneName = rung.Name,
                MonetaryAmount = rung.MonetaryAmount,
                LeaveDaysBonus = rung.LeaveDaysBonus,
                HighestGrantedYears = held,
                Standing = isExempt ? LongServiceStanding.Exempt : LongServiceStanding.Eligible,
                Reason = isExempt ? "A disciplinary record exempts this employee from the award." : null,
            });
        }

        return verdicts;
    }

    /// <summary>
    /// How far short of the next rung somebody is — a useful thing for a report to say, and a
    /// pointless thing for it to make the reader work out.
    /// </summary>
    private static string NextRungMessage(IReadOnlyList<LongServiceMilestone> rungs, int years)
    {
        var next = rungs.FirstOrDefault(m => m.Years > years);
        return next == null
            ? "No milestone applies to this employee's length of service."
            : $"{next.Years - years} year(s) short of the {next.Years}-year milestone.";
    }

    /// <summary>
    /// Employees carrying a disciplinary record that counts against them.
    /// </summary>
    /// <remarks>
    /// <para><b>Draft and Dismissed are excluded, and that is a reading of "negative record" rather
    /// than a softening of it.</b> A draft case is one nobody has been formally accused in; a
    /// dismissed case is an exoneration. Counting either would deny an award over an allegation that
    /// went nowhere, which is not what TDC asked for.</para>
    ///
    /// <para>A case <c>UnderAppeal</c> does count: the decision stands until it is overturned.</para>
    ///
    /// <para>The date used is the decision date where there is one, falling back to the incident
    /// date — a case still working through the process has no decision yet, and dating it by when it
    /// was raised is the only honest option.</para>
    /// </remarks>
    private async Task<HashSet<Guid>> LoadDisqualifiedAsync(Guid tenantId, DateTime asOf, int? months)
    {
        var query = _context.Set<StaffDisciplinaryAction>()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted
                && a.Status != DisciplinaryStatus.Draft
                && a.Status != DisciplinaryStatus.Dismissed);

        if (months is { } window)
        {
            var since = asOf.AddMonths(-window);
            query = query.Where(a => (a.DecisionDate ?? a.IncidentDate) >= since);
        }

        return (await query.Select(a => a.EmployeeId).Distinct().ToListAsync()).ToHashSet();
    }
}
