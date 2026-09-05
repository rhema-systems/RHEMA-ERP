using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Workflow status adapter for <see cref="StaffDisciplinaryAction"/>.
///
/// <para><b>What is on the engine is the DECISION, not the case.</b> A disciplinary case has many
/// writers across its life — the investigator, the hearing officer, the employee acknowledging a
/// notice, the appellant — which is exactly the reason <c>GoalStatus</c> and <c>AppraisalStatus</c>
/// stayed off the engine. But one segment of it is a pure single-writer approval lifecycle: an
/// officer proposes a sanction, and somebody with the authority to do so confirms it. That segment
/// is what this adapter owns, and nothing else.</para>
///
/// <para>Routing here is a real policy choice, which is the other half of the test. A verbal warning
/// for lateness and a summary dismissal for gross misconduct are not the same decision and must not
/// take the same route — FR-HR-080 limits heads of department to verbal warnings, and FR-HR-092 has
/// the MD signing every termination bar the procedural ones. <c>SimpleWorkflowService</c> therefore
/// puts the severity, the offence, the proposed action type and its authority level into the entity
/// context, so a definition can branch on them.</para>
///
/// <para><b>No new status member was needed.</b> The proposals and the PIP each had to gain a
/// <c>PendingApproval</c>; <see cref="DisciplinaryStatus"/> already has
/// <see cref="DisciplinaryStatus.AwaitingDecision"/>, which means precisely "a decision has been
/// proposed and is awaiting confirmation". Look before you add — the sixth application of this
/// recipe made the same call about <c>StaffRequisitionStatus.Submitted</c>.</para>
///
/// <para><b>Rejection returns the case to <see cref="DisciplinaryStatus.UnderReview"/>, not to a
/// rejected state.</b> This differs from the movement adapter deliberately. Refusing a proposed
/// sanction does not refuse the case — the allegation still stands and still has to be answered, so
/// the case goes back for a different decision rather than ending. There is no "Rejected" member on
/// this enum for the same reason: a disciplinary case ends by being closed or dismissed, and
/// "dismissed" here means the allegation was dropped, which is a finding, not a refusal.
/// The refusal itself is on the workflow record, with its reason and its author.</para>
///
/// <para>Recall lands on <c>UnderReview</c> too. A recalled decision was never refused by anybody;
/// the officer simply took it back, and the case is where it was before they proposed anything.</para>
///
/// <para>Everything after confirmation — issuing the sanction, the appeal, closure — stays a direct
/// action on the record. Recording that a warning letter was issued is not an approval.</para>
///
/// <para>Adapters are auto-discovered by the assembly scan in
/// <c>WorkflowServiceCollectionExtensions</c>; there is no DI registration to add.</para>
/// </summary>
public sealed class StaffDisciplinaryActionWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "StaffDisciplinaryAction",
        "Staff Disciplinary Action",
        "STAFF_DISCIPLINARY_ACTION"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, userId);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
        => Apply(Require(entity), outcome, userId);

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
        => Apply(Require(entity), WorkflowOutcome.Recalled, userId);

    private static void Apply(StaffDisciplinaryAction disciplinaryCase, WorkflowOutcome outcome, Guid? userId)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                // The decision is now final. DecisionDate and DecisionById were stamped when the
                // officer proposed it — this records that it was confirmed, not re-authored.
                disciplinaryCase.Status = DisciplinaryStatus.DecisionMade;
                break;

            case WorkflowOutcome.Rejected:
            case WorkflowOutcome.Recalled:
                // Back for reconsideration, and the proposed sanction is cleared with it. Leaving
                // ActionTypeId set on a case that is no longer proposing anything would show the
                // refused sanction on the record as though it stood.
                disciplinaryCase.Status = DisciplinaryStatus.UnderReview;
                disciplinaryCase.ActionTypeId = null;
                disciplinaryCase.ActionDetails = null;
                disciplinaryCase.DecisionDate = null;
                disciplinaryCase.DecisionById = null;
                disciplinaryCase.DecisionRationale = null;
                break;

            default:
                disciplinaryCase.Status = DisciplinaryStatus.AwaitingDecision;
                break;
        }

        disciplinaryCase.UpdatedAt = DateTime.UtcNow;
        if (userId.HasValue)
            disciplinaryCase.UpdatedBy = userId.Value.ToString();
    }

    private static StaffDisciplinaryAction Require(object entity)
        => entity as StaffDisciplinaryAction
           ?? throw new InvalidOperationException("Expected StaffDisciplinaryAction entity.");
}
