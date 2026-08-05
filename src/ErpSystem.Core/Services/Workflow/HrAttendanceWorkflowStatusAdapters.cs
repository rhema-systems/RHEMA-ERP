using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Workflow status adapters for the four approvable Attendance &amp; Time entities:
/// attendance regularizations, overtime requests, remote-work requests and consultant
/// timesheets.
///
/// Adapters are auto-discovered by the assembly scan in
/// <c>WorkflowServiceCollectionExtensions</c>; there is no DI registration to add.
///
/// A note on the three attendance statuses below: unlike <c>LeaveRequest</c> they have no
/// <c>Draft</c> member, so the record is created already sitting at <c>Pending</c> and the
/// approval workflow starts at creation time rather than on a separate submit. That makes
/// Pending do double duty as both "not yet submitted" and "awaiting a decision", and it is
/// why <see cref="IWorkflowStatusAdapter.ApplyRecallOutcome"/> returns them to Pending —
/// there is no earlier state to fall back to. Consultant timesheets do have a Draft, so
/// they behave exactly like leave.
/// </summary>
public sealed class StaffAttendanceRegularizationWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "StaffAttendanceRegularization",
        "Attendance Regularization",
        "STAFF_ATTENDANCE_REGULARIZATION"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, userId);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var regularization = Require(entity);
        Apply(regularization, outcome, userId);

        if (outcome == WorkflowOutcome.Rejected && !string.IsNullOrWhiteSpace(rejectionReason))
        {
            regularization.RejectionReason = rejectionReason.Trim();
        }
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
        => Apply(Require(entity), WorkflowOutcome.Recalled, userId);

    private static void Apply(StaffAttendanceRegularization regularization, WorkflowOutcome outcome, Guid? userId)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                // Approved, not Applied — writing the corrected times back onto the daily
                // attendance record is a separate, explicit step (POST .../apply).
                regularization.Status = AttendanceRegularizationStatus.Approved;
                regularization.ApprovedById = userId;
                regularization.ApprovalDate = DateTime.UtcNow;
                regularization.RejectedDate = null;
                regularization.RejectionReason = null;
                break;
            case WorkflowOutcome.Rejected:
                regularization.Status = AttendanceRegularizationStatus.Rejected;
                regularization.ApprovedById = null;
                regularization.ApprovalDate = null;
                regularization.RejectedDate = DateTime.UtcNow;
                break;
            default:
                // Pending covers both Recalled and the awaiting-approval case.
                regularization.Status = AttendanceRegularizationStatus.Pending;
                regularization.ApprovedById = null;
                regularization.ApprovalDate = null;
                regularization.RejectedDate = null;
                regularization.RejectionReason = null;
                break;
        }
    }

    private static StaffAttendanceRegularization Require(object entity)
        => entity as StaffAttendanceRegularization
           ?? throw new InvalidOperationException("Expected StaffAttendanceRegularization entity.");
}

public sealed class StaffOvertimeRequestWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "StaffOvertimeRequest",
        "Overtime Request",
        "STAFF_OVERTIME_REQUEST"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, userId);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var request = Require(entity);
        Apply(request, outcome, userId);

        if (outcome == WorkflowOutcome.Rejected && !string.IsNullOrWhiteSpace(rejectionReason))
        {
            request.RejectionReason = rejectionReason.Trim();
        }
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
        => Apply(Require(entity), WorkflowOutcome.Recalled, userId);

    private static void Apply(StaffOvertimeRequest request, WorkflowOutcome outcome, Guid? userId)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                // Approved is pre-approval only. Completed is reached later, when the
                // supervisor confirms the hours actually worked.
                request.Status = OvertimeRequestStatus.Approved;
                request.ApprovedById = userId;
                request.ApprovalDate = DateTime.UtcNow;
                request.RejectedDate = null;
                request.RejectionReason = null;
                break;
            case WorkflowOutcome.Rejected:
                request.Status = OvertimeRequestStatus.Rejected;
                request.ApprovedById = null;
                request.ApprovalDate = null;
                request.RejectedDate = DateTime.UtcNow;
                break;
            default:
                request.Status = OvertimeRequestStatus.Pending;
                request.ApprovedById = null;
                request.ApprovalDate = null;
                request.RejectedDate = null;
                request.RejectionReason = null;
                break;
        }
    }

    private static StaffOvertimeRequest Require(object entity)
        => entity as StaffOvertimeRequest
           ?? throw new InvalidOperationException("Expected StaffOvertimeRequest entity.");
}

public sealed class RemoteWorkRequestWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "RemoteWorkRequest",
        "Remote Work Request",
        "REMOTE_WORK_REQUEST"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, userId);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var request = Require(entity);
        Apply(request, outcome, userId);

        if (outcome == WorkflowOutcome.Rejected && !string.IsNullOrWhiteSpace(rejectionReason))
        {
            request.RejectionReason = rejectionReason.Trim();
        }
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
        => Apply(Require(entity), WorkflowOutcome.Recalled, userId);

    private static void Apply(RemoteWorkRequest request, WorkflowOutcome outcome, Guid? userId)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                request.Status = RemoteWorkRequestStatus.Approved;
                request.ApprovedById = userId;
                request.ApprovalDate = DateTime.UtcNow;
                request.RejectedDate = null;
                request.RejectionReason = null;
                break;
            case WorkflowOutcome.Rejected:
                request.Status = RemoteWorkRequestStatus.Rejected;
                request.ApprovedById = null;
                request.ApprovalDate = null;
                request.RejectedDate = DateTime.UtcNow;
                break;
            default:
                // Cancelled is a user action, never a workflow outcome, so recall lands
                // back on Pending rather than cancelling the request.
                request.Status = RemoteWorkRequestStatus.Pending;
                request.ApprovedById = null;
                request.ApprovalDate = null;
                request.RejectedDate = null;
                request.RejectionReason = null;
                break;
        }
    }

    private static RemoteWorkRequest Require(object entity)
        => entity as RemoteWorkRequest
           ?? throw new InvalidOperationException("Expected RemoteWorkRequest entity.");
}

public sealed class ConsultantTimesheetWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "ConsultantTimesheet",
        "Consultant Timesheet",
        "CONSULTANT_TIMESHEET"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
    {
        var timesheet = Require(entity);
        Apply(timesheet, outcome);

        if (outcome != WorkflowOutcome.Recalled)
        {
            timesheet.SubmittedDate = DateTime.UtcNow;
        }
    }

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
        => Apply(Require(entity), outcome);

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var timesheet = Require(entity);
        timesheet.Status = TimesheetStatus.Draft;
        timesheet.SubmittedDate = null;
    }

    private static void Apply(ConsultantTimesheet timesheet, WorkflowOutcome outcome)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                // Approved internally. SentToClient / ClientConfirmed come later, from the
                // tokenised client-confirmation round-trip, which is not a workflow step.
                timesheet.Status = TimesheetStatus.Approved;
                break;
            case WorkflowOutcome.Rejected:
                timesheet.Status = TimesheetStatus.Rejected;
                break;
            case WorkflowOutcome.Recalled:
                timesheet.Status = TimesheetStatus.Draft;
                timesheet.SubmittedDate = null;
                break;
            default:
                timesheet.Status = TimesheetStatus.Submitted;
                break;
        }
    }

    private static ConsultantTimesheet Require(object entity)
        => entity as ConsultantTimesheet
           ?? throw new InvalidOperationException("Expected ConsultantTimesheet entity.");
}
