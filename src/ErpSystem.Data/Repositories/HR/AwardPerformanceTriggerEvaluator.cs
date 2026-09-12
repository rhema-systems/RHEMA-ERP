using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Awards;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

/// <summary>
/// Finds the employees an award should put forward automatically, from their performance record.
/// </summary>
/// <remarks>
/// <para>TDC's note: <i>"some of the nomination will be due to performance or target reached"</i>.
/// Two triggers, configured per award as <c>MinPerformanceScore</c> and <c>MinGoalsAchieved</c>, and
/// combining when both are set.</para>
///
/// <para><b>What the data can support today, measured 2026-08-21.</b> The appraisal store holds
/// <b>4,328</b> appraisals of which <b>18</b> carry an <c>OverallScore</c>; <b>16</b> employees hold
/// a goal at 100%. So a real generation run finds a handful of people out of 5,579. That is the
/// unmaintained-column shape this module keeps meeting, and the rule is correct regardless — but the
/// caller is told how many records were even examined, so "almost nobody qualified" can be told
/// apart from "almost nobody has been appraised".</para>
///
/// <para><b>Only the most recent scored appraisal counts.</b> An employee with three appraisals is
/// judged on the latest one that has a score, not on their best ever — an award for current
/// performance should not be won on a result from four years ago.</para>
/// </remarks>
public class AwardPerformanceTriggerEvaluator : IAwardPerformanceTriggerEvaluator
{
    private readonly ApplicationDbContext _context;

    public AwardPerformanceTriggerEvaluator(ApplicationDbContext context) => _context = context;

    public async Task<AwardPerformanceTriggerResult> EvaluateAsync(AwardType awardType, Guid tenantId)
    {
        var result = new AwardPerformanceTriggerResult
        {
            MinPerformanceScore = awardType.MinPerformanceScore,
            MinGoalsAchieved = awardType.MinGoalsAchieved,
        };

        if (awardType.MinPerformanceScore == null && awardType.MinGoalsAchieved == null)
            return result;

        // ── the score trigger ─────────────────────────────────────────────────
        HashSet<Guid>? byScore = null;
        if (awardType.MinPerformanceScore is { } minScore)
        {
            var scored = await _context.Set<PerformanceAppraisal>()
                .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.OverallScore != null)
                .Select(a => new { a.EmployeeId, a.OverallScore, a.CreatedAt })
                .ToListAsync();

            result.AppraisalsExamined = scored.Count;

            // Latest scored appraisal per employee, not their best.
            byScore = scored
                .GroupBy(a => a.EmployeeId)
                .Select(g => g.OrderByDescending(a => a.CreatedAt).First())
                .Where(a => a.OverallScore >= minScore)
                .Select(a => a.EmployeeId)
                .ToHashSet();
        }

        // ── the goal trigger ──────────────────────────────────────────────────
        HashSet<Guid>? byGoals = null;
        if (awardType.MinGoalsAchieved is { } minGoals)
        {
            var completed = await _context.Set<EmployeeGoal>()
                .Where(g => g.TenantId == tenantId && !g.IsDeleted && g.ProgressPercent >= 100)
                .GroupBy(g => g.EmployeeId)
                .Select(g => new { EmployeeId = g.Key, Count = g.Count() })
                .ToListAsync();

            result.GoalsExamined = completed.Sum(x => x.Count);

            byGoals = completed
                .Where(x => x.Count >= minGoals)
                .Select(x => x.EmployeeId)
                .ToHashSet();
        }

        // Both set means both must be satisfied — "performance AND target reached" is a stricter
        // award than either alone, and an award that asked for both should not settle for one.
        result.EmployeeIds = (byScore, byGoals) switch
        {
            ({ } s, { } g) => s.Intersect(g).ToList(),
            ({ } s, null) => s.ToList(),
            (null, { } g) => g.ToList(),
            _ => new List<Guid>(),
        };

        return result;
    }
}
