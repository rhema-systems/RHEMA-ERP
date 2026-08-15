using ErpSystem.Core.Entities.HR.PromotionTransfer;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Workflow status adapter for <see cref="StaffMovement"/>.
///
/// <para>A movement belongs on the generic engine for the same reason the two outcome proposals and
/// the PIP do, and <c>GoalStatus</c> and <c>AppraisalStatus</c> do not: Draft → Submitted → Approved
/// is a pure approval lifecycle with a single writer. Everything after approval — acceptance,
/// handover, implementation, the return from a temporary assignment — is the movement being carried
/// out, has several writers, and stays a direct action on the record.</para>
///
/// <para>Routing is a real policy decision, which is the other half of the test. A lateral move
/// inside one department and a promotion across two of them with a 30% rise are not the same
/// decision, so <c>SimpleWorkflowService</c> puts the type, the category, the salary change and BOTH
/// sides' supervisor and head-of-department ids into the entity context. That last part is what this
/// area needs and a role-based chain could not express: a movement has an origin and a destination,
/// and both have to agree to it.</para>
///
/// <para><b>The six intermediate statuses.</b> <see cref="StaffMovementStatus"/> names a route —
/// CurrentSupervisorApproval, NewSupervisorApproval, CurrentHodApproval, NewHodApproval, HrReview,
/// ManagementApproval. Nothing has ever assigned them, and this adapter does not either: which step
/// a movement is on is the workflow instance's business, and the instance knows it exactly, whereas
/// this enum could only ever guess from a step name a tenant is free to choose. A movement in flight
/// is <c>Submitted</c>, and the screens read the live step from the workflow record. The members stay
/// on the enum so the pending-approval queue keeps matching any row a previous build left behind.</para>
///
/// <para>Rejection returns the movement to <c>Rejected</c> rather than to Draft — unlike a PIP, a
/// movement carries a rejection reason and a rejected-by on the entity itself, and HR raising a
/// fresh request is the intended path. Recall returns it to <c>Draft</c> for the requester to
/// rework.</para>
///
/// <para>Adapters are auto-discovered by the assembly scan in
/// <c>WorkflowServiceCollectionExtensions</c>; there is no DI registration to add.</para>
/// </summary>
public sealed class StaffMovementWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "StaffMovement",
        "Staff Movement",
        "STAFF_MOVEMENT"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, userId, null);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
        => Apply(Require(entity), outcome, userId, rejectionReason);

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
        => Apply(Require(entity), WorkflowOutcome.Recalled, userId, reason);

    private static void Apply(StaffMovement movement, WorkflowOutcome outcome, Guid? userId, string? reason)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                movement.Status = StaffMovementStatus.Approved;
                movement.AuthorizationDate = DateTime.UtcNow;
                break;

            case WorkflowOutcome.Rejected:
                movement.Status = StaffMovementStatus.Rejected;
                movement.RejectionDate = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(reason))
                    movement.RejectionReason = reason.Trim();
                break;

            case WorkflowOutcome.Recalled:
                // Back to the requester, and the rejection fields are cleared: a recalled movement
                // was never refused by anybody, and leaving a stale reason on it would say it was.
                movement.Status = StaffMovementStatus.Draft;
                movement.RejectionReason = null;
                movement.RejectedById = null;
                movement.RejectionDate = null;
                break;

            default:
                movement.Status = StaffMovementStatus.Submitted;
                break;
        }
    }

    private static StaffMovement Require(object entity)
        => entity as StaffMovement ?? throw new InvalidOperationException("Expected StaffMovement entity.");
}
