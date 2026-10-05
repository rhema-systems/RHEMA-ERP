using ErpSystem.Core.Entities.HR.CompanySchedule;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// The workflow status adapter for a company event that needs approval (company-schedule final closure,
/// lane 2b — decision D-10). Auto-discovered by the assembly scan in
/// <c>WorkflowServiceCollectionExtensions</c>; there is no DI registration to add.
/// </summary>
/// <remarks>
/// <para><b>No Draft, no PendingApproval.</b> An event is created Scheduled and the approval starts at
/// creation, as the attendance requests do. "Awaiting approval" is <c>RequiresApproval</c> with no
/// <c>ApprovalDate</c> — no new status, so the diaries, the sweep and the clash check read it as they
/// already do (<see cref="ErpSystem.Core.Services.HR.CompanyEventRules.IsAwaitingApproval"/>).</para>
///
/// <para><b>Recall is written out.</b> The interface's default sets a "Draft" status by reflection, and
/// <see cref="EventStatus"/> has none, so it would silently do nothing. A recalled event goes back to
/// awaiting approval; a confirmed one back to scheduled.</para>
///
/// <para><b>A rejected event is cancelled</b>, with the reason as its cancellation reason. An event has
/// no other state for "this is not going ahead"; the service cancels its room bookings and tells
/// everybody invited, as a cancellation does.</para>
///
/// <para>⚠ The <c>userId</c> handed in is the approver's EMPLOYEE id: <c>ApprovedById</c> is an
/// Employee foreign key, as on the attendance requests.</para>
/// </remarks>
public sealed class CompanyEventWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "CompanyEvent",
        "Company Event",
        "COMPANY_EVENT",
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, userId, reason: null);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
        => Apply(Require(entity), outcome, userId, rejectionReason);

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
        => Apply(Require(entity), WorkflowOutcome.Recalled, userId, reason);

    private static void Apply(CompanyEvent e, WorkflowOutcome outcome, Guid? userId, string? reason)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                e.ApprovedById = userId;
                e.ApprovalDate = DateTime.UtcNow;
                // A postponed event stays postponed — approved, with no date to confirm.
                if (e.Status != EventStatus.Postponed) e.Status = EventStatus.Confirmed;
                break;

            case WorkflowOutcome.Rejected:
                e.ApprovedById = null;
                e.ApprovedBy = null;
                e.ApprovalDate = null;
                e.IsCancelled = true;
                e.CancellationDate = DateTime.UtcNow;
                var why = string.IsNullOrWhiteSpace(reason) ? "Not approved." : $"Not approved: {reason.Trim()}";
                e.CancellationReason = why.Length > 1000 ? why[..1000] : why;
                e.Status = EventStatus.Cancelled;
                break;

            default:
                // Pending (submitted, or a stage passed with more to come) and Recalled: awaiting approval.
                e.ApprovedById = null;
                e.ApprovedBy = null;
                e.ApprovalDate = null;
                if (e.Status == EventStatus.Confirmed) e.Status = EventStatus.Scheduled;
                break;
        }
    }

    private static CompanyEvent Require(object entity)
        => entity as CompanyEvent
           ?? throw new InvalidOperationException("Expected a CompanyEvent entity.");
}
