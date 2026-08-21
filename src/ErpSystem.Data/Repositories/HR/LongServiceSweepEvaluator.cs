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
/// Finds who has reached a long-service milestone, and who a disciplinary record disqualifies.
/// </summary>
/// <remarks>
/// <para><b>Service is counted in completed years</b> via <c>HrPolicyCalculations.CompletedYears</c>
/// — the single implementation area 14 slice 3b consolidated. The arithmetic matters more here than
/// anywhere else in the module: a milestone is a threshold, and the old calendar-year subtraction
/// would have granted a ten-year award to somebody with nine years and one month.</para>
///
/// <para>⚠ <b>Measured 2026-08-21, and this shapes what a live run can show.</b> Of 5,579 employees,
/// <b>2,103</b> carry a <c>DateEmployed</c> at all, exactly <b>one</b> has ten completed years, and
/// <b>none</b> has fifteen. A live sweep therefore finds at most one person. That is a fact about
/// TDC's employee records, not about this engine, and the result reports how many employees it could
/// even measure so the two cannot be confused.</para>
///
/// <para><b>An employee with no employment date is reported, not skipped.</b> They are neither
/// qualified nor disqualified — the system cannot tell — and 62% of the workforce is in that state.
/// Silently omitting them would make a sweep of 5,579 people look like a sweep of 2,103.</para>
/// </remarks>
public class LongServiceSweepEvaluator : ILongServiceSweepEvaluator
{
    private readonly ApplicationDbContext _context;

    public LongServiceSweepEvaluator(ApplicationDbContext context) => _context = context;

    public async Task<LongServiceSweepResult> EvaluateAsync(
        AwardType awardType, IReadOnlyList<LongServiceMilestone> ladder, Guid tenantId, DateTime asOf)
    {
        var result = new LongServiceSweepResult { AsOf = asOf };

        if (ladder.Count == 0)
            return result;

        var employees = await _context.Set<Employee>()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted && e.TerminationDate == null)
            .Select(e => new
            {
                e.Id,
                e.FirstName,
                e.LastName,
                e.EmployeeNumber,
                e.DateEmployed,
            })
            .ToListAsync();

        result.EmployeesConsidered = employees.Count;
        result.WithoutEmploymentDate = employees.Count(e => e.DateEmployed == null);

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

        var disqualified = awardType.DisqualifyOnDisciplinaryRecord
            ? await LoadDisqualifiedAsync(tenantId, asOf, awardType.DisqualifyingDisciplineMonths)
            : new HashSet<Guid>();

        result.DisciplinaryRecordsConsidered = disqualified.Count;

        var rungs = ladder.Where(m => m.IsActive).OrderBy(m => m.Years).ToList();

        foreach (var employee in employees)
        {
            if (employee.DateEmployed == null) continue;

            var years = HrPolicyCalculations.CompletedYears(employee.DateEmployed, DateOnly.FromDateTime(asOf)) ?? 0;

            // The highest rung reached that stands above everything this employee already holds.
            // Sweeping every rung at once would hand somebody who joined twenty years ago four
            // awards in one run; sweeping the ones below would hand them out one per run afterwards.
            var ceiling = highestGranted.TryGetValue(employee.Id, out var held) ? held : 0;

            var rung = rungs
                .Where(m => years >= m.Years && m.Years > ceiling)
                .OrderByDescending(m => m.Years)
                .FirstOrDefault();

            if (rung == null) continue;

            var name = $"{employee.FirstName} {employee.LastName}".Trim();

            if (disqualified.Contains(employee.Id))
            {
                result.Disqualified.Add(new LongServiceCandidate
                {
                    EmployeeId = employee.Id,
                    EmployeeName = name,
                    EmployeeNumber = employee.EmployeeNumber,
                    YearsOfService = years,
                    MilestoneYears = rung.Years,
                    MilestoneId = rung.Id,
                    Reason = "A disciplinary record disqualifies this employee from the award.",
                });
                continue;
            }

            result.Qualified.Add(new LongServiceCandidate
            {
                EmployeeId = employee.Id,
                EmployeeName = name,
                EmployeeNumber = employee.EmployeeNumber,
                YearsOfService = years,
                MilestoneYears = rung.Years,
                MilestoneId = rung.Id,
                ServiceStartDate = employee.DateEmployed.Value,
            });
        }

        return result;
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
