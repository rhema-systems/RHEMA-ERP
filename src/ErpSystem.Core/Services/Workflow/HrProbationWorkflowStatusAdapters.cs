using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Workflow status adapter for <see cref="ProbationPeriod"/> confirmation (area 15b slice 8b).
/// </summary>
/// <remarks>
/// <para><b>Why probation confirmation belongs on the engine.</b> FRD FR-HR-032 states the chain
/// exactly — <i>system (month 5) → head confirms → HR issues the confirmation letter</i>. The middle
/// step is an approval by a named person who is not HR, which is what the engine is for; and slice
/// 8a gives that person a resolvable identity (the confirming-authority map), without which the
/// step would route to nobody for 93% of employees.</para>
///
/// <para><b>Approval stops at <see cref="ProbationStatus.ConfirmationApproved"/>, not
/// <c>Completed</c>.</b> That is deliberate and is the same call the proposals and PIP made: an
/// adapter is synchronous and sees only the entity, so it cannot write the employee record —
/// and confirmation's whole point is that it clears <c>StaffStatus.Probation</c> and stamps
/// <c>ConfirmationDate</c>. Letting the adapter set <c>Completed</c> would produce a probation that
/// reads as confirmed while the employee still reads as on probation, which is precisely the
/// divergence slice 5 converged. So the engine owns the decision, and HR's confirm call — one
/// service method both this route and the direct route reach — owns the consequences.</para>
///
/// <para><b>Rejection returns to <see cref="ProbationStatus.Active"/>, and there is no Rejected
/// status.</b> A movement can be rejected and die; a probation cannot. Declining to confirm does not
/// end anything — the employee is still on probation, and someone must now extend or terminate it.
/// Sending it back to Active says exactly that, and the reminder engine picks the case up again the
/// next morning rather than letting it fall silent.</para>
///
/// <para>Adapters are auto-discovered by the assembly scan in
/// <c>WorkflowServiceCollectionExtensions</c>; there is no DI registration to add.</para>
/// </remarks>
public sealed class ProbationPeriodWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "ProbationPeriod",
        "Probation Period",
        "PROBATION_PERIOD"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, null);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
        => Apply(Require(entity), outcome, rejectionReason);

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
        => Apply(Require(entity), WorkflowOutcome.Recalled, reason);

    private static void Apply(ProbationPeriod probation, WorkflowOutcome outcome, string? reason)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                // The decision, not the consequences. HR's confirm call finishes the job.
                probation.Status = ProbationStatus.ConfirmationApproved;
                if (!string.IsNullOrWhiteSpace(reason))
                    probation.OutcomeNotes = reason.Trim();
                break;

            case WorkflowOutcome.Rejected:
                // Back to Active: the probation continues, and now needs an extension or a
                // termination. Nothing about the employee's record changes on a rejection.
                probation.Status = ProbationStatus.Active;
                probation.OutcomeNotes = string.IsNullOrWhiteSpace(reason)
                    ? "Confirmation declined; the probation remains open."
                    : $"Confirmation declined: {reason.Trim()}";
                break;

            case WorkflowOutcome.Recalled:
                probation.Status = ProbationStatus.Active;
                break;

            case WorkflowOutcome.Pending:
            default:
                probation.Status = ProbationStatus.PendingConfirmation;
                break;
        }
    }

    private static ProbationPeriod Require(object entity)
        => entity as ProbationPeriod
           ?? throw new InvalidOperationException(
               $"{nameof(ProbationPeriodWorkflowStatusAdapter)} received {entity?.GetType().Name ?? "null"}.");
}
