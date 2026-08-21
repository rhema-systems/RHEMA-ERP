using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Awards;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

/// <summary>
/// Applies every eligibility criterion an award type carries, and says why anyone is excluded.
/// </summary>
/// <remarks>
/// See <see cref="IAwardEligibilityEvaluator"/> for why this exists. The criteria are applied in a
/// deliberate order — cheap facts about the employee first, then the target scoping, then the
/// counting rules that need a second query — so the common case does the least work.
/// </remarks>
public class AwardEligibilityEvaluator : IAwardEligibilityEvaluator
{
    private readonly ApplicationDbContext _context;

    public AwardEligibilityEvaluator(ApplicationDbContext context) => _context = context;

    public async Task<AwardEligibilityResult> EvaluateAsync(Guid awardTypeId, Guid tenantId, DateTime asOf)
    {
        var awardType = await LoadTypeAsync(awardTypeId, tenantId);
        if (awardType == null)
            return new AwardEligibilityResult { AwardTypeId = awardTypeId, AsOf = asOf };

        var targets = await LoadTargetsAsync(awardTypeId, tenantId, asOf);
        var employees = await LoadEmployeesAsync(tenantId);
        var priorWins = await LoadWinCountsAsync(awardTypeId, tenantId);

        var eligible = new List<AwardEligibilityVerdict>();
        var ineligible = new List<AwardEligibilityVerdict>();

        foreach (var employee in employees)
        {
            var verdict = Judge(awardType, targets, employee, priorWins, asOf);
            (verdict.IsEligible ? eligible : ineligible).Add(verdict);
        }

        return new AwardEligibilityResult
        {
            AwardTypeId = awardTypeId,
            AsOf = asOf,
            ConsideredCount = employees.Count,
            Eligible = eligible.OrderBy(v => v.EmployeeName).ToList(),
            Ineligible = ineligible.OrderBy(v => v.EmployeeName).ToList()
        };
    }

    public async Task<AwardEligibilityVerdict> EvaluateEmployeeAsync(
        Guid awardTypeId, Guid employeeId, Guid tenantId, DateTime asOf)
    {
        var awardType = await LoadTypeAsync(awardTypeId, tenantId);
        var employee = await _context.Set<Employee>()
            .Include(e => e.Position)
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.TenantId == tenantId && !e.IsDeleted);

        if (awardType == null || employee == null)
        {
            return new AwardEligibilityVerdict
            {
                EmployeeId = employeeId,
                IsEligible = false,
                Reasons = { awardType == null ? "The award type does not exist." : "The employee does not exist." }
            };
        }

        var targets = await LoadTargetsAsync(awardTypeId, tenantId, asOf);
        var priorWins = await LoadWinCountsAsync(awardTypeId, tenantId);
        return Judge(awardType, targets, employee, priorWins, asOf);
    }

    // ── loading ───────────────────────────────────────────────────────────────

    private Task<AwardType?> LoadTypeAsync(Guid awardTypeId, Guid tenantId)
        => _context.Set<AwardType>()
            .FirstOrDefaultAsync(t => t.Id == awardTypeId && t.TenantId == tenantId && !t.IsDeleted);

    /// <summary>
    /// Targets in force at <paramref name="asOf"/>. The effective dating on
    /// <c>AwardTypeTarget</c> existed and was never honoured — a target that expired last year still
    /// scoped the award.
    /// </summary>
    private async Task<List<AwardTypeTarget>> LoadTargetsAsync(Guid awardTypeId, Guid tenantId, DateTime asOf)
        => await _context.Set<AwardTypeTarget>()
            .Where(t => t.AwardTypeId == awardTypeId && t.TenantId == tenantId && !t.IsDeleted
                && (t.EffectiveFrom == null || t.EffectiveFrom <= asOf)
                && (t.EffectiveTo == null || t.EffectiveTo >= asOf))
            .ToListAsync();

    private async Task<List<Employee>> LoadEmployeesAsync(Guid tenantId)
        => await _context.Set<Employee>()
            .Include(e => e.Position)
            .Where(e => e.TenantId == tenantId && !e.IsDeleted && e.TerminationDate == null)
            .ToListAsync();

    private async Task<Dictionary<Guid, int>> LoadWinCountsAsync(Guid awardTypeId, Guid tenantId)
        => await _context.Set<EmployeeAward>()
            .Where(a => a.AwardTypeId == awardTypeId && a.TenantId == tenantId && !a.IsDeleted)
            .GroupBy(a => a.EmployeeId)
            .Select(g => new { EmployeeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EmployeeId, x => x.Count);

    // ── judging ───────────────────────────────────────────────────────────────

    private static AwardEligibilityVerdict Judge(
        AwardType awardType,
        List<AwardTypeTarget> targets,
        Employee employee,
        Dictionary<Guid, int> priorWins,
        DateTime asOf)
    {
        var reasons = new List<string>();

        // 1. Service. This is the criterion the old check omitted entirely, and it is the one TDC
        //    is most likely to set. An employee with no DateEmployed is not silently eligible:
        //    "we cannot tell" is said out loud, because 62% of the live records are in that state.
        if (awardType.MinServiceYears.HasValue || awardType.MaxServiceYears.HasValue)
        {
            if (employee.DateEmployed == null)
            {
                reasons.Add("Length of service cannot be checked: the employee has no employment date on record.");
            }
            else
            {
                var years = HrPolicyCalculations.CompletedYears(employee.DateEmployed, DateOnly.FromDateTime(asOf)) ?? 0;
                if (awardType.MinServiceYears.HasValue && years < awardType.MinServiceYears)
                    reasons.Add($"Requires {awardType.MinServiceYears} years' service; this employee has {years}.");
                if (awardType.MaxServiceYears.HasValue && years > awardType.MaxServiceYears)
                    reasons.Add($"Limited to {awardType.MaxServiceYears} years' service; this employee has {years}.");
            }
        }

        // 2. Age, from the award type. Distinct from the per-target age range below: this one
        //    applies to the whole award, the other narrows a single target.
        if (awardType.MinAge.HasValue || awardType.MaxAge.HasValue)
        {
            if (employee.DateOfBirth == null)
            {
                reasons.Add("Age cannot be checked: the employee has no date of birth on record.");
            }
            else
            {
                var age = HrPolicyCalculations.CompletedYears(employee.DateOfBirth, DateOnly.FromDateTime(asOf)) ?? 0;
                if (awardType.MinAge.HasValue && age < awardType.MinAge)
                    reasons.Add($"Requires age {awardType.MinAge}; this employee is {age}.");
                if (awardType.MaxAge.HasValue && age > awardType.MaxAge)
                    reasons.Add($"Limited to age {awardType.MaxAge}; this employee is {age}.");
            }
        }

        // 3. Target scoping — exclusions first, then inclusions.
        if (targets.Count > 0)
        {
            var excluded = targets.Where(t => t.IsExclusion).FirstOrDefault(t => Matches(t, employee, asOf));
            if (excluded != null)
            {
                reasons.Add(string.IsNullOrWhiteSpace(excluded.Reason)
                    ? $"Excluded by an award rule on {excluded.TargetType}."
                    : $"Excluded by an award rule on {excluded.TargetType}: {excluded.Reason}");
            }

            var inclusions = targets.Where(t => !t.IsExclusion).ToList();
            if (inclusions.Count > 0 && !inclusions.Any(t => Matches(t, employee, asOf)))
                reasons.Add("Outside the units, positions, levels or people this award is scoped to.");
        }

        // 4. How many times this employee may win it.
        if (awardType.MaxAwardsPerEmployee.HasValue
            && priorWins.TryGetValue(employee.Id, out var won)
            && won >= awardType.MaxAwardsPerEmployee)
        {
            reasons.Add($"Already won this award {won} time(s); the limit is {awardType.MaxAwardsPerEmployee}.");
        }

        return new AwardEligibilityVerdict
        {
            EmployeeId = employee.Id,
            EmployeeName = $"{employee.FirstName} {employee.LastName}".Trim(),
            EmployeeNumber = employee.EmployeeNumber,
            IsEligible = reasons.Count == 0,
            Reasons = reasons
        };
    }

    private static bool Matches(AwardTypeTarget target, Employee employee, DateTime asOf)
    {
        var typeMatches = target.TargetType switch
        {
            AwardTargetType.OrganizationUnit => target.TargetId == employee.OrganizationUnitId,
            AwardTargetType.Position => target.TargetId == employee.PositionId,
            AwardTargetType.StaffLevel => target.TargetId == employee.Position?.StaffLevelId,
            AwardTargetType.Employee => target.TargetId == employee.Id,
            _ => false
        };

        if (!typeMatches) return false;

        if ((target.MinAge.HasValue || target.MaxAge.HasValue) && employee.DateOfBirth.HasValue)
        {
            var age = HrPolicyCalculations.CompletedYears(employee.DateOfBirth, DateOnly.FromDateTime(asOf)) ?? 0;
            if (target.MinAge.HasValue && age < target.MinAge) return false;
            if (target.MaxAge.HasValue && age > target.MaxAge) return false;
        }

        return true;
    }

}
