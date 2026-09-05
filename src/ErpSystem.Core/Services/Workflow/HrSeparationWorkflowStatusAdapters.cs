using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Workflow status adapter for <see cref="EmployeeSeparation"/> — the FR-HR-092 signature on an
/// exit.
///
/// <para>An exit belongs on the generic engine for the reason W1 gives: the Managing Director signs
/// a great many things, and should sign them all in one inbox rather than visiting each module's
/// own screen. Draft → PendingApproval → Approved/Rejected is a pure approval lifecycle with a
/// single writer, which is the shape the engine models.</para>
///
/// <para><b>The adapter owns exactly the three states before clearance, and nothing after.</b>
/// Everything from <c>ClearanceInProgress</c> onwards — the clearance run, the settlement, Internal
/// Audit's review, completion — has several writers and is the exit being <i>worked</i>, not
/// approved. Those stay direct actions on the record, each guarded by its own precondition. This
/// mirrors the call made for the outcome proposals: leave the terminal steps off the engine, because
/// reaching them records that somebody did the work, not that anybody approved it.</para>
///
/// <para><b>No new enum member was needed.</b> <c>SeparationStatus</c> already carried
/// <c>PendingApproval</c>, <c>Approved</c> and <c>Rejected</c>, meaning precisely what the engine's
/// Pending, Approved and Rejected outcomes mean. Look before adding one: the proposals and the PIP
/// each needed a <c>PendingApproval</c>; the requisition did not, and neither does this.</para>
///
/// <para><b>Rejection lands on <c>Rejected</c>, not back on <c>Draft</c>.</b> The service already
/// treats a rejected separation as editable and re-submittable, and the distinction is worth
/// keeping: it tells the initiator that somebody <i>ruled against</i> this exit rather than that
/// they never sent it. Recall is the initiator withdrawing before anyone ruled, so that does go back
/// to <c>Draft</c>.</para>
///
/// <para><b>What this adapter must never write.</b> <c>Cancelled</c> is somebody abandoning an exit,
/// and <c>Completed</c> is the employee record having been updated — decisions and events, not
/// workflow outcomes. Nothing here touches them.</para>
///
/// <para>⚠ The engine decides <i>whether this user may approve at this step</i>. It does not decide
/// <b>FR-HR-092</b>, which is a rule about the RECORD: the Managing Director may sign any exit, HR
/// only a procedural one. That check stays in <c>SeparationService.RequireDecisionAuthority</c> and
/// runs first, so a refusal explains itself in terms of the separation rather than answering the
/// generic "you are not assigned as an approver". A routing rule in the definition can express the
/// same split for assignment, but the guarantee lives in the service.</para>
///
/// <para>Adapters are auto-discovered by the assembly scan in
/// <c>WorkflowServiceCollectionExtensions</c>; there is no DI registration to add.</para>
/// </summary>
public sealed class EmployeeSeparationWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "EmployeeSeparation",
        "Employee Separation",
        "EMPLOYEE_SEPARATION"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, null);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
        => Apply(Require(entity), outcome, rejectionReason);

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
        => Apply(Require(entity), WorkflowOutcome.Recalled, reason);

    private static void Apply(EmployeeSeparation separation, WorkflowOutcome outcome, string? reason)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                separation.Status = SeparationStatus.Approved;
                separation.ApprovedOn ??= DateTime.UtcNow;
                break;

            case WorkflowOutcome.Rejected:
                separation.Status = SeparationStatus.Rejected;
                separation.RejectedOn ??= DateTime.UtcNow;
                // ⚠ Only where the service has not already recorded one. RejectAsync requires a
                // reason and writes it; this is the path where the engine rejects on its own (a
                // step timing out, an approver acting from the generic inbox) and would otherwise
                // leave a refused exit with no stated grounds — the thing that gets resubmitted
                // unchanged because nobody can tell what was wrong with it.
                if (string.IsNullOrWhiteSpace(separation.RejectionReason)
                    && !string.IsNullOrWhiteSpace(reason))
                    separation.RejectionReason = Trim(reason, 1000);
                break;

            case WorkflowOutcome.Recalled:
                // Withdrawn before anyone ruled, so the submission itself is undone: a separation
                // sitting at Draft with a submitted-by stamp on it would read as still in flight.
                separation.Status = SeparationStatus.Draft;
                separation.SubmittedOn = null;
                separation.SubmittedById = null;
                break;

            default:
                separation.Status = SeparationStatus.PendingApproval;
                separation.SubmittedOn ??= DateTime.UtcNow;
                break;
        }
    }

    private static string Trim(string value, int max)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private static EmployeeSeparation Require(object entity)
        => entity as EmployeeSeparation
           ?? throw new ArgumentException(
               $"Expected an {nameof(EmployeeSeparation)} but received "
               + $"{entity?.GetType().Name ?? "null"}.", nameof(entity));
}
