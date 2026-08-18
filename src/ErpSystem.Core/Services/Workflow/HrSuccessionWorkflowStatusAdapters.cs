using ErpSystem.Core.Entities.HR.SuccessionPlanning;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Workflow status adapter for <see cref="SuccessionPlan"/>.
///
/// <para>A succession plan belongs on the generic engine for the same reason the requisition, the
/// two outcome proposals and the PIP do: Draft → UnderReview → Approved/Rejected is a pure approval
/// lifecycle with a single writer. Everything after approval — nominating candidates, assessing
/// them, running their development, superseding the plan with a later version — is the plan being
/// *worked*, has many writers, and stays a direct action on the record.</para>
///
/// <para><b>Routing is a real policy decision here</b>, which is the other half of the test for
/// whether an approval belongs on the engine at all. A succession plan for a Low-criticality post
/// with a ready-now successor and one for a Critical post at HighRisk with nobody identified should
/// not go to the same approver. <c>SimpleWorkflowService</c> therefore puts criticality, risk
/// level, the successor count and whether emergency cover exists into the entity context for a
/// definition to branch on.</para>
///
/// <para><b>No new enum member was needed.</b> Following the requisition lesson — look before you
/// add — <c>SuccessionPlanStatus</c> already carries <c>UnderReview</c>, which means exactly "out
/// for approval". The proposals and the PIP each needed a <c>PendingApproval</c> because they had
/// no such state; this one does.</para>
///
/// <para>Rejection lands on <c>Rejected</c> rather than back on <c>Draft</c>, as the requisition
/// does: a rejected plan is editable and re-submittable, and keeping the state distinct is what
/// tells the author that someone ruled against this plan rather than that they never sent it.
/// Recall — the author withdrawing before anyone ruled — goes back to <c>Draft</c>.</para>
///
/// <para><b>The terminal states are deliberately off the engine.</b> <c>Archived</c> is written by
/// <c>ApproveAsync</c> when a successor version supersedes this one, and <c>Completed</c> records
/// that the succession actually happened — a person moved into the post. Neither is anybody
/// approving anything, so neither is the engine's to write. This is the same line the proposals
/// drew at Applied/Actioned.</para>
///
/// <para>Adapters are auto-discovered by the assembly scan in
/// <c>WorkflowServiceCollectionExtensions</c>; there is no DI registration to add.</para>
/// </summary>
public sealed class SuccessionPlanWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "SuccessionPlan",
        "Succession Plan",
        "SUCCESSION_PLAN"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, null);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
        => Apply(Require(entity), outcome, rejectionReason, userId);

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
        => Apply(Require(entity), WorkflowOutcome.Recalled, reason);

    private static void Apply(SuccessionPlan plan, WorkflowOutcome outcome, string? reason, Guid? userId = null)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                plan.Status = SuccessionPlanStatus.Approved;
                plan.ApprovalDate = DateTime.UtcNow;

                // ⚠ The engine hands back the approving USER; ApprovedById is an Employee FK. The
                // service resolves the employee and stamps it, exactly as the bespoke path did —
                // the same actor-id mismatch recorded for the attendance area. Left null here
                // rather than written with the wrong id.
                break;

            case WorkflowOutcome.Rejected:
                plan.Status = SuccessionPlanStatus.Rejected;
                if (!string.IsNullOrWhiteSpace(reason))
                    plan.RiskAssessmentNotes = Append(plan.RiskAssessmentNotes, $"Approval rejected: {reason.Trim()}");
                break;

            case WorkflowOutcome.Recalled:
                plan.Status = SuccessionPlanStatus.Draft;
                break;

            default:
                plan.Status = SuccessionPlanStatus.UnderReview;
                break;
        }
    }

    /// <summary>
    /// The rejection reason is also written onto the plan's risk notes.
    /// </summary>
    /// <remarks>
    /// It is recorded on the workflow history row too, but the notes are what the author sees on
    /// the plan they are about to rework — and a rejected plan with no stated reason is the one
    /// that comes back resubmitted unchanged. Trimmed to the 2000-char column.
    /// </remarks>
    private static string Append(string? existing, string addition)
    {
        var combined = string.IsNullOrWhiteSpace(existing) ? addition : $"{existing}\n{addition}";
        return combined.Length > 2000 ? combined[..2000] : combined;
    }

    private static SuccessionPlan Require(object entity)
        => entity as SuccessionPlan ?? throw new InvalidOperationException("Expected SuccessionPlan entity.");
}
