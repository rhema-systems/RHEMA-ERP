using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Workflow status adapter for <see cref="TeamTermsOfReference"/> approval (round 2, lane F3).
/// </summary>
/// <remarks>
/// <para><b>Why a committee's charter belongs on the engine.</b> Terms of reference say what a
/// committee may decide and on whose authority — the audit committee's power to call for records,
/// the tender board's threshold, the disciplinary panel's standing. Signing that off is not an HR
/// filing act, and who signs it differs by which unit the committee serves. F1 shipped an
/// <c>approve</c> endpoint that asked only whether the caller could write to the team, which meant
/// <b>the lead could approve their own charter</b> — a committee granting itself its own authority.
/// The engine, plus the record-level authority check in the service, ends that.</para>
///
/// <para><b>Rejection returns to <see cref="TeamTorStatus.Draft"/>, not to a terminal state.</b> A
/// charter is a document under revision, never a request that dies: declining to approve one sends
/// it back to the committee to be reworked, which is what <c>Draft</c> already means here. The
/// reason is kept — see <see cref="TeamTermsOfReference.RejectionReason"/> for why that column had
/// to exist. Recall is the committee withdrawing its own submission before anyone has ruled, and
/// lands in the same place, with no reason recorded against it because nobody refused anything.</para>
///
/// <para><b>⚠ The adapter sets the decision; the service applies the consequences.</b> Approving a
/// charter must also <b>supersede the version it replaces</b> — moving the predecessor to
/// <see cref="TeamTorStatus.Superseded"/> and closing its <c>EffectiveTo</c> — and an adapter is
/// synchronous and sees only the one entity it is handed, so it cannot touch siblings. That matters
/// more than it looks: every read that answers "what is this committee chartered to do" selects the
/// single approved version, and two approved versions do not merely read oddly, they make the
/// question unanswerable. <c>TeamActivityService.ApproveTermsAsync</c> stays the one place the
/// consequences run — the same split the job-description and probation adapters make.</para>
///
/// <para><b>⚠ <c>ApprovedById</c> is left alone here.</b> The engine hands back the approving
/// <i>user</i>; <c>ApprovedById</c> is an Employee FK. Writing a user id into it would be worse
/// than leaving it null — see the attendance actor conventions — so the service resolves and
/// stamps the employee.</para>
///
/// <para>Adapters are auto-discovered by the assembly scan in
/// <c>WorkflowServiceCollectionExtensions</c>; there is no DI registration to add.</para>
/// </remarks>
public sealed class TeamTermsOfReferenceWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    // ⚠ Prefixed `Hr`, deliberately, like the HR asset types. `TermsOfReference` is a phrase other
    // modules could plausibly claim, and the flat entity-type namespace is shared across the whole
    // system: under a bare name an approver's notification would deep-link to somebody else's
    // screen and nothing would error.
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "HrTeamTermsOfReference",
        "HR Team Terms Of Reference",
        "HR_TEAM_TERMS_OF_REFERENCE"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, null);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
        => Apply(Require(entity), outcome, rejectionReason);

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
        => Apply(Require(entity), WorkflowOutcome.Recalled, reason);

    private static void Apply(TeamTermsOfReference terms, WorkflowOutcome outcome, string? reason)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                // The decision only. ApproveTermsAsync supersedes the standing version and stamps
                // the approving employee — see the remarks for why neither can happen here.
                terms.Status = TeamTorStatus.Approved;
                terms.RejectionReason = null;
                break;

            case WorkflowOutcome.Rejected:
                // Back to the committee. A refused charter is unfinished, not dead.
                terms.Status = TeamTorStatus.Draft;
                terms.RejectionReason = string.IsNullOrWhiteSpace(reason)
                    ? "Refused without a stated reason."
                    : reason.Trim();
                break;

            case WorkflowOutcome.Recalled:
                // Withdrawn by the committee before anyone ruled. No reason is recorded against a
                // rejection that never happened.
                terms.Status = TeamTorStatus.Draft;
                terms.RejectionReason = null;
                break;

            case WorkflowOutcome.Pending:
            default:
                terms.Status = TeamTorStatus.PendingApproval;
                break;
        }
    }

    private static TeamTermsOfReference Require(object entity)
        => entity as TeamTermsOfReference
           ?? throw new InvalidOperationException(
               $"{nameof(TeamTermsOfReferenceWorkflowStatusAdapter)} received {entity?.GetType().Name ?? "null"}.");
}

/// <summary>
/// Workflow status adapter for <see cref="TeamObjective"/> approval (round 2, lane F3).
/// </summary>
/// <remarks>
/// <para><b>Why an objective is approved and a task is not.</b> An objective is what a committee
/// undertakes to deliver in a period, and it is the thing a review is later written against — so
/// somebody outside the team has an interest in whether it is the right undertaking, and in whether
/// its weight, target and deadline are honest. A task is how the team goes about it, and belongs
/// entirely to the team. Only the first crosses the boundary the engine exists to police, which is
/// why F3 wires the objective and leaves the board alone.</para>
///
/// <para><b>The adapter owns exactly three states and nothing after.</b> <c>Draft</c>,
/// <c>PendingApproval</c> and the move to <c>Active</c>; everything from there — <c>OnHold</c>,
/// <c>Completed</c>, <c>Cancelled</c> — is the team running its own work and stays a direct action
/// on the record. This is the split the PIP adapter made, for the same reason: a single-writer
/// approval lifecycle in front of a many-writer working phase.</para>
///
/// <para><b>⚠ The direct status route is CLOSED for these two transitions.</b>
/// <c>ChangeObjectiveStatusAsync</c> refuses <c>Draft → Active</c> and any hand-set
/// <c>PendingApproval</c>. Leaving that door open would have made the whole lane decorative — a
/// lead who found the approval inconvenient could simply set the status themselves, and nothing
/// would have said so.</para>
///
/// <para><b>Rejection returns to <see cref="TeamObjectiveStatus.Draft"/> with its reason kept</b>,
/// in <see cref="TeamObjective.RejectionReason"/> rather than <c>CancelledReason</c>: an objective
/// that was refused before it started and one the team stopped pursuing are different facts, and a
/// year later nobody can tell them apart from a single column.</para>
/// </remarks>
public sealed class TeamObjectiveWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "HrTeamObjective",
        "HR Team Objective",
        "HR_TEAM_OBJECTIVE"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, null);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
        => Apply(Require(entity), outcome, rejectionReason);

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
        => Apply(Require(entity), WorkflowOutcome.Recalled, reason);

    private static void Apply(TeamObjective objective, WorkflowOutcome outcome, string? reason)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                // ⚠ Active, not "Approved" — the objective has no such state, and rightly: an
                // approved objective is one the team is now working on.
                objective.Status = TeamObjectiveStatus.Active;
                objective.RejectionReason = null;
                break;

            case WorkflowOutcome.Rejected:
                objective.Status = TeamObjectiveStatus.Draft;
                objective.RejectionReason = string.IsNullOrWhiteSpace(reason)
                    ? "Refused without a stated reason."
                    : reason.Trim();
                break;

            case WorkflowOutcome.Recalled:
                objective.Status = TeamObjectiveStatus.Draft;
                objective.RejectionReason = null;
                break;

            case WorkflowOutcome.Pending:
            default:
                objective.Status = TeamObjectiveStatus.PendingApproval;
                break;
        }
    }

    private static TeamObjective Require(object entity)
        => entity as TeamObjective
           ?? throw new InvalidOperationException(
               $"{nameof(TeamObjectiveWorkflowStatusAdapter)} received {entity?.GetType().Name ?? "null"}.");
}
