using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Maps the engine's outcome onto an <see cref="EmployeeSalaryChangeRequest"/> (round 3, lane S —
/// the tenth application of the recipe). Recall goes back to Draft; the service applies the
/// change when the outcome is Approved, never this adapter. Auto-discovered by the assembly scan.
/// </summary>
public sealed class HrSalaryChangeWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "HrEmployeeSalaryChangeRequest",
        "HR Employee Salary Change Request",
        "HR_EMPLOYEE_SALARY_CHANGE_REQUEST",
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, null);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
        => Apply(Require(entity), outcome, rejectionReason);

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
        => Apply(Require(entity), WorkflowOutcome.Recalled, reason);

    private static void Apply(EmployeeSalaryChangeRequest request, WorkflowOutcome outcome, string? reason)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                request.Status = SalaryChangeRequestStatus.Approved;
                break;
            case WorkflowOutcome.Rejected:
                request.Status = SalaryChangeRequestStatus.Rejected;
                request.RejectionReason = string.IsNullOrWhiteSpace(reason) ? "Rejected" : reason.Trim();
                break;
            case WorkflowOutcome.Recalled:
                request.Status = SalaryChangeRequestStatus.Draft;
                break;
            default:
                request.Status = SalaryChangeRequestStatus.PendingApproval;
                break;
        }
    }

    private static EmployeeSalaryChangeRequest Require(object entity)
        => entity as EmployeeSalaryChangeRequest
           ?? throw new InvalidOperationException("Expected EmployeeSalaryChangeRequest entity.");
}
