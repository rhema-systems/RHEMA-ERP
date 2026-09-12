using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Workflow status adapter for <see cref="PerformanceImprovementPlan"/>.
///
/// <para>A PIP belongs on the generic engine for the same reason <c>AppraisalTemplate</c> and the
/// two outcome proposals do, and <c>GoalStatus</c> and <c>AppraisalStatus</c> do not: Draft →
/// PendingApproval → Active is a pure approval lifecycle with a single writer. Everything after
/// that — InProgress, the review meetings, the outcome — is the plan running, has several writers,
/// and stays a direct action on the record.</para>
///
/// <para>Routing is a real policy decision here, which is the other half of the test. A 30-day
/// plan a line manager writes and a 6-month one that ends in a termination recommendation should
/// not go to the same approver, so <c>SimpleWorkflowService</c> puts the duration, the supervisor
/// and whether the plan came out of an appraisal into the entity context for a definition to
/// branch on.</para>
///
/// <para>Rejection and recall both return the plan to <c>Draft</c>: the author gets it back to
/// rework, which is the only sensible place for a plan that was not agreed. Nothing here writes
/// <c>Cancelled</c> — abandoning a plan is a decision someone makes, not a workflow outcome.</para>
///
/// <para>Adapters are auto-discovered by the assembly scan in
/// <c>WorkflowServiceCollectionExtensions</c>; there is no DI registration to add.</para>
/// </summary>
public sealed class PerformanceImprovementPlanWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "PerformanceImprovementPlan",
        "Performance Improvement Plan",
        "PERFORMANCE_IMPROVEMENT_PLAN",
        "PIP"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, null);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
        => Apply(Require(entity), outcome, rejectionReason);

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
        => Apply(Require(entity), WorkflowOutcome.Recalled, reason);

    private static void Apply(PerformanceImprovementPlan plan, WorkflowOutcome outcome, string? reason)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                plan.Status = PipStatus.Active;
                break;
            case WorkflowOutcome.Rejected:
                plan.Status = PipStatus.Draft;
                if (!string.IsNullOrWhiteSpace(reason))
                    plan.ReviewSchedule = Append(plan.ReviewSchedule, $"Approval rejected: {reason.Trim()}");
                break;
            case WorkflowOutcome.Recalled:
                plan.Status = PipStatus.Draft;
                break;
            default:
                plan.Status = PipStatus.PendingApproval;
                break;
        }
    }

    /// <summary>
    /// The rejection reason is kept on <c>ReviewSchedule</c>, the plan's free-text working notes —
    /// there is no dedicated notes column, and losing why an approver refused it would leave the
    /// author with a plan back in draft and no idea what to change. Trimmed to the 2000-char column.
    /// </summary>
    private static string Append(string? existing, string addition)
    {
        var combined = string.IsNullOrWhiteSpace(existing) ? addition : $"{existing}\n{addition}";
        return combined.Length > 2000 ? combined[..2000] : combined;
    }

    private static PerformanceImprovementPlan Require(object entity)
        => entity as PerformanceImprovementPlan ?? throw new InvalidOperationException("Expected PerformanceImprovementPlan entity.");
}
