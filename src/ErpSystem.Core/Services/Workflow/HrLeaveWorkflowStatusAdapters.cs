using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Workflow status adapters for the HR leave and training-nomination entities.
///
/// These four services already drove the generic workflow engine
/// (<c>LeaveService</c>, <c>LeaveEncashmentService</c>, <c>LeavePlanService</c>,
/// <c>TrainingNominationService</c>) but no adapter implemented their entity types, so
/// <c>WorkflowStatusAdapterRegistry.GetAdapter</c> threw immediately after the workflow
/// call succeeded — submit and approve failed at runtime. Adapters are auto-discovered by
/// the assembly scan in <c>WorkflowServiceCollectionExtensions</c>; no DI registration.
/// </summary>
public sealed class LeaveRequestWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "LeaveRequest",
        "Leave Request",
        "LEAVE_REQUEST"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, userId);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var request = Require(entity);
        if (IsFinal(request)) return;
        Apply(request, outcome, userId);

        if (outcome == WorkflowOutcome.Rejected && !string.IsNullOrWhiteSpace(rejectionReason))
        {
            request.RejectionReason = rejectionReason.Trim();
        }
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var request = Require(entity);
        if (IsFinal(request)) return;
        request.Status = LeaveStatus.Draft;
        request.ApprovedById = null;
        request.ApprovedDate = null;
        request.RejectionReason = null;
    }

    /// <summary>
    /// Cancelled and closed leave is final: no workflow outcome moves it (round 5, lane D).
    /// </summary>
    /// <remarks>
    /// Until lane D, cancelling a Pending request left its workflow instance live. An approver could
    /// then still act on it, and the generic workflow recall applies this adapter directly,
    /// bypassing LeaveService, so a cancelled request could come back as Draft, or as Approved.
    /// Cancelling now withdraws the instance, but instances already stranded by the old cancel
    /// remain, and this is the one place every door passes through.
    /// </remarks>
    private static bool IsFinal(LeaveRequest request)
        => request.Status is LeaveStatus.Cancelled or LeaveStatus.Completed;

    private static void Apply(LeaveRequest request, WorkflowOutcome outcome, Guid? userId)
    {
        if (IsFinal(request)) return;

        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                request.Status = LeaveStatus.Approved;
                request.ApprovedById = userId;
                request.ApprovedDate = DateTime.UtcNow;
                request.RejectionReason = null;
                break;
            case WorkflowOutcome.Rejected:
                request.Status = LeaveStatus.Rejected;
                request.ApprovedById = null;
                request.ApprovedDate = null;
                break;
            case WorkflowOutcome.Recalled:
                request.Status = LeaveStatus.Draft;
                request.ApprovedById = null;
                request.ApprovedDate = null;
                request.RejectionReason = null;
                break;
            default:
                // Pending: awaiting one or more approval steps.
                request.Status = LeaveStatus.Pending;
                request.ApprovedById = null;
                request.ApprovedDate = null;
                request.RejectionReason = null;
                break;
        }
    }

    private static LeaveRequest Require(object entity)
        => entity as LeaveRequest ?? throw new InvalidOperationException("Expected LeaveRequest entity.");
}

public sealed class LeaveEncashmentWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "LeaveEncashment",
        "Leave Encashment",
        "LEAVE_ENCASHMENT"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, userId);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var encashment = Require(entity);
        Apply(encashment, outcome, userId);

        if (outcome == WorkflowOutcome.Rejected && !string.IsNullOrWhiteSpace(rejectionReason))
        {
            encashment.RejectionReason = rejectionReason.Trim();
        }
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var encashment = Require(entity);
        encashment.Status = LeaveEncashmentStatus.Draft;
        encashment.ApprovedById = null;
        encashment.ApprovedDate = null;
        encashment.RejectionReason = null;
    }

    private static void Apply(LeaveEncashment encashment, WorkflowOutcome outcome, Guid? userId)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                // Approved, not Processed — payment is a separate downstream step.
                encashment.Status = LeaveEncashmentStatus.Approved;
                encashment.ApprovedById = userId;
                encashment.ApprovedDate = DateTime.UtcNow;
                encashment.RejectionReason = null;
                break;
            case WorkflowOutcome.Rejected:
                encashment.Status = LeaveEncashmentStatus.Rejected;
                encashment.ApprovedById = null;
                encashment.ApprovedDate = null;
                break;
            case WorkflowOutcome.Recalled:
                encashment.Status = LeaveEncashmentStatus.Draft;
                encashment.ApprovedById = null;
                encashment.ApprovedDate = null;
                encashment.RejectionReason = null;
                break;
            default:
                encashment.Status = LeaveEncashmentStatus.PendingApproval;
                encashment.ApprovedById = null;
                encashment.ApprovedDate = null;
                encashment.RejectionReason = null;
                break;
        }
    }

    private static LeaveEncashment Require(object entity)
        => entity as LeaveEncashment ?? throw new InvalidOperationException("Expected LeaveEncashment entity.");
}

public sealed class LeavePlanWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "LeavePlan",
        "Leave Plan",
        "LEAVE_PLAN"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, userId);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var plan = Require(entity);
        Apply(plan, outcome, userId);

        if (outcome == WorkflowOutcome.Rejected && !string.IsNullOrWhiteSpace(rejectionReason))
        {
            plan.RejectionReason = rejectionReason.Trim();
        }
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var plan = Require(entity);
        plan.Status = LeavePlanStatus.Draft;
        plan.ApprovedById = null;
        plan.ApprovedDate = null;
        plan.RejectionReason = null;
    }

    private static void Apply(LeavePlan plan, WorkflowOutcome outcome, Guid? userId)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                plan.Status = LeavePlanStatus.Approved;
                plan.ApprovedById = userId;
                plan.ApprovedDate = DateTime.UtcNow;
                plan.RejectionReason = null;
                break;
            case WorkflowOutcome.Rejected:
                plan.Status = LeavePlanStatus.Rejected;
                plan.ApprovedById = null;
                plan.ApprovedDate = null;
                break;
            case WorkflowOutcome.Recalled:
                plan.Status = LeavePlanStatus.Draft;
                plan.ApprovedById = null;
                plan.ApprovedDate = null;
                plan.RejectionReason = null;
                break;
            default:
                // LeavePlanStatus has no dedicated pending member; Submitted is the
                // awaiting-decision state.
                plan.Status = LeavePlanStatus.Submitted;
                plan.ApprovedById = null;
                plan.ApprovedDate = null;
                plan.RejectionReason = null;
                break;
        }
    }

    private static LeavePlan Require(object entity)
        => entity as LeavePlan ?? throw new InvalidOperationException("Expected LeavePlan entity.");
}

public sealed class TrainingNominationWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "TrainingNomination",
        "Training Nomination",
        "TRAINING_NOMINATION"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, userId);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var nomination = Require(entity);
        Apply(nomination, outcome, userId);

        if (outcome == WorkflowOutcome.Rejected)
        {
            nomination.RejectedDate = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(rejectionReason))
            {
                nomination.RejectionReason = rejectionReason.Trim();
            }
        }
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var nomination = Require(entity);
        nomination.Status = NominationStatus.Draft;
        nomination.HrApprovedById = null;
        nomination.HrApprovalDate = null;
        nomination.RejectedDate = null;
        nomination.RejectionReason = null;
    }

    private static void Apply(TrainingNomination nomination, WorkflowOutcome outcome, Guid? userId)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                // The engine owns the routing; reaching Approved means every configured
                // step (supervisor, HR) has passed, so HR approval is what is stamped.
                nomination.Status = NominationStatus.Approved;
                nomination.HrApprovedById = userId;
                nomination.HrApprovalDate = DateTime.UtcNow;
                nomination.RejectedDate = null;
                nomination.RejectionReason = null;
                break;
            case WorkflowOutcome.Rejected:
                nomination.Status = NominationStatus.Rejected;
                nomination.HrApprovedById = null;
                nomination.HrApprovalDate = null;
                break;
            case WorkflowOutcome.Recalled:
                nomination.Status = NominationStatus.Draft;
                nomination.HrApprovedById = null;
                nomination.HrApprovalDate = null;
                nomination.RejectedDate = null;
                nomination.RejectionReason = null;
                break;
            default:
                nomination.Status = NominationStatus.Submitted;
                nomination.HrApprovedById = null;
                nomination.HrApprovalDate = null;
                nomination.RejectedDate = null;
                nomination.RejectionReason = null;
                break;
        }
    }

    private static TrainingNomination Require(object entity)
        => entity as TrainingNomination ?? throw new InvalidOperationException("Expected TrainingNomination entity.");
}
