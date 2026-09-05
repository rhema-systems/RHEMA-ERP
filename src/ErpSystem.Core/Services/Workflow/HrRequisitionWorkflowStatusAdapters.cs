using ErpSystem.Core.Entities.HR.Requisition;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Workflow status adapter for <see cref="StaffRequisition"/>.
///
/// <para>A headcount requisition belongs on the generic engine for the same reason the two outcome
/// proposals and <c>PerformanceImprovementPlan</c> do, and <c>GoalStatus</c> and
/// <c>AppraisalStatus</c> do not: Draft → Submitted → Approved/Rejected is a pure approval
/// lifecycle with a single writer. Everything after approval — linking a vacancy, recording costs,
/// fulfilment as people are hired — is the requisition being worked and has several writers, so it
/// stays a direct action on the record.</para>
///
/// <para>Routing is a real policy decision here, which is the other half of the test. One
/// replacement head a manager asks for and a five-head expansion that no manpower budget covers
/// should not go to the same approver, so <c>SimpleWorkflowService</c> puts the headcount, the
/// requisition type, the priority and whether the request is over budget into the entity context
/// for a definition to branch on.</para>
///
/// <para><c>Submitted</c> is the "out for approval" state — the enum already carried it, so unlike
/// <c>PipStatus</c> no new member was needed. <c>UnderReview</c> is left alone: it is a state a
/// reviewer sets by hand while they gather information, and the engine never writes it.</para>
///
/// <para>Rejection lands on <c>Rejected</c>, not back on <c>Draft</c>: the service already treats
/// Rejected as editable and re-submittable, and keeping it distinct is what lets a requester see
/// that someone ruled against this request rather than that they never sent it. Recall is the
/// requester withdrawing before anyone ruled, so that does go back to <c>Draft</c>. Nothing here
/// writes <c>Cancelled</c> or <c>OnHold</c> — abandoning or parking a requisition is a decision
/// someone makes, not a workflow outcome.</para>
///
/// <para>Adapters are auto-discovered by the assembly scan in
/// <c>WorkflowServiceCollectionExtensions</c>; there is no DI registration to add.</para>
/// </summary>
public sealed class StaffRequisitionWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "StaffRequisition",
        "Staff Requisition",
        "STAFF_REQUISITION"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, null);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
        => Apply(Require(entity), outcome, rejectionReason);

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
        => Apply(Require(entity), WorkflowOutcome.Recalled, reason);

    private static void Apply(StaffRequisition requisition, WorkflowOutcome outcome, string? reason)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                requisition.Status = StaffRequisitionStatus.Approved;
                break;
            case WorkflowOutcome.Rejected:
                requisition.Status = StaffRequisitionStatus.Rejected;
                if (!string.IsNullOrWhiteSpace(reason))
                    requisition.Notes = Append(requisition.Notes, $"Approval rejected: {reason.Trim()}");
                break;
            case WorkflowOutcome.Recalled:
                requisition.Status = StaffRequisitionStatus.Draft;
                break;
            default:
                requisition.Status = StaffRequisitionStatus.Submitted;
                break;
        }
    }

    /// <summary>
    /// The rejection reason is also written to the requisition's notes. It is recorded on the
    /// history row too, but the notes are what the requester sees on the form they are about to
    /// rework, and a rejected requisition with no stated reason is the thing that gets resubmitted
    /// unchanged. Trimmed to the 1000-char column.
    /// </summary>
    private static string Append(string? existing, string addition)
    {
        var combined = string.IsNullOrWhiteSpace(existing) ? addition : $"{existing}\n{addition}";
        return combined.Length > 1000 ? combined[..1000] : combined;
    }

    private static StaffRequisition Require(object entity)
        => entity as StaffRequisition ?? throw new InvalidOperationException("Expected StaffRequisition entity.");
}
