using ErpSystem.Core.Entities.HR.JobAnalysis;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Workflow status adapter for <see cref="JobDescription"/> approval (area 17 slice 3).
/// </summary>
/// <remarks>
/// <para><b>Why job-description approval belongs on the engine.</b> FRD FR-HR-134 asks the system
/// to maintain <i>approved</i> job descriptions against positions, and an approved job description
/// is not an HR filing act: it fixes what a role is accountable for, and it carries the valuation
/// and the suggested salary grade that a pay decision later rests on. Who signs that off differs by
/// job family and by level, which is precisely what a workflow definition expresses and what a
/// hard-coded role check cannot. Before this slice the bespoke <c>approve</c> endpoint asked only
/// whether the caller held an Admin permission.</para>
///
/// <para><b>Rejection returns to <see cref="JobDescriptionStatus.UnderRevision"/>, not to a
/// terminal state.</b> A job description is a document under revision, never a request that dies:
/// declining to approve one means it goes back to its author to be reworked, and the status enum
/// already has the word for that. Recall returns it to <see cref="JobDescriptionStatus.Draft"/>,
/// which is where a withdrawn document belongs.</para>
///
/// <para><b>⚠ The adapter sets the decision; the service applies the consequences.</b> Approving a
/// job description must also supersede the version it replaces — stamping
/// <c>SupersededByVersionId</c>, moving the predecessor to
/// <see cref="JobDescriptionStatus.Superseded"/> and expiring it — and an adapter is synchronous
/// and sees only the one entity it is handed, so it cannot touch siblings. That matters more here
/// than it looks: <c>OfferLetterService</c> selects a position's job description by
/// <c>SupersededByVersionId == null</c>, so two unsuperseded approved versions do not merely read
/// oddly, they make an offer letter ambiguous. <c>JobDescriptionService.ApproveAsync</c> therefore
/// remains the single place the consequences run, on both the engine route and the direct one —
/// the same split the probation and proposals adapters make.</para>
///
/// <para><b>⚠ <c>ApprovedById</c> is left alone here.</b> The engine hands back the approving
/// <i>user</i>; <c>ApprovedById</c> is an Employee FK. Writing the user id into it would be worse
/// than leaving it — see the attendance actor conventions — so the service resolves and stamps it.</para>
///
/// <para>Adapters are auto-discovered by the assembly scan in
/// <c>WorkflowServiceCollectionExtensions</c>; there is no DI registration to add.</para>
/// </remarks>
public sealed class JobDescriptionWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "JobDescription",
        "Job Description",
        "JOB_DESCRIPTION"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, null);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
        => Apply(Require(entity), outcome, rejectionReason);

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
        => Apply(Require(entity), WorkflowOutcome.Recalled, reason);

    private static void Apply(JobDescription jobDescription, WorkflowOutcome outcome, string? reason)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                // The decision only. ApproveAsync supersedes the predecessor, stamps the approver
                // and sets the next review date — see the remarks above for why none of that can
                // happen here.
                jobDescription.Status = JobDescriptionStatus.Approved;
                jobDescription.ApprovalDate = DateTime.UtcNow;
                break;

            case WorkflowOutcome.Rejected:
                // Back to the author. A rejected job description is not dead, it is unfinished.
                jobDescription.Status = JobDescriptionStatus.UnderRevision;
                if (!string.IsNullOrWhiteSpace(reason))
                    jobDescription.RevisionReason = reason.Trim();
                break;

            case WorkflowOutcome.Recalled:
                jobDescription.Status = JobDescriptionStatus.Draft;
                break;

            case WorkflowOutcome.Pending:
            default:
                jobDescription.Status = JobDescriptionStatus.PendingReview;
                break;
        }
    }

    private static JobDescription Require(object entity)
        => entity as JobDescription
           ?? throw new InvalidOperationException(
               $"{nameof(JobDescriptionWorkflowStatusAdapter)} received {entity?.GetType().Name ?? "null"}.");
}

/// <summary>
/// Workflow status adapter for <see cref="ManpowerBudget"/> approval (area 17/18 slice 7).
/// </summary>
/// <remarks>
/// <para><b>FRD FR-HR-135 names the chain outright</b> — <i>Department Head → HR → Managing
/// Director</i> — which is a three-step approval that no permission model can express. It is also
/// the requirement that makes this the sharpest workflow integration in the area: what is being
/// approved is <b>headcount and the money behind it</b>, and once approved the budget becomes the
/// establishment that gates whether a vacancy may be opened at all (FR-HR-136, decision D-2).</para>
///
/// <para><b>Rejection keeps its reason.</b> <c>RejectAsync</c> previously took a reason, set the
/// status, and discarded it — a budget holder could see their budget had been refused with no way
/// to find out why. The adapter writes it to <c>RejectionReason</c>, and so does the direct route.</para>
///
/// <para><b>⚠ <c>ApprovedById</c> is left alone here</b>, as on the job description: the engine
/// hands back the approving <i>user</i> and <c>ApprovedById</c> is an Employee FK. The service
/// resolves and stamps the employee.</para>
///
/// <para><b>Rejection returns to <see cref="ManpowerBudgetStatus.Rejected"/>, which is terminal,
/// and that is the right shape here.</b> Unlike a job description — a document that goes back for
/// revision — a rejected budget for a fiscal year is a decision: the department is told no, and a
/// different budget is a different submission. Recall returns to Draft, which is the route back for
/// a submitter who wants to change their own numbers before anyone rules on them.</para>
/// </remarks>
public sealed class ManpowerBudgetWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "ManpowerBudget",
        "Manpower Budget",
        "MANPOWER_BUDGET"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, null);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
        => Apply(Require(entity), outcome, rejectionReason);

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
        => Apply(Require(entity), WorkflowOutcome.Recalled, reason);

    private static void Apply(ManpowerBudget budget, WorkflowOutcome outcome, string? reason)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                // The decision only. The service stamps the approver and applies the establishment.
                budget.Status = ManpowerBudgetStatus.Approved;
                budget.ApprovalDate = DateTime.UtcNow;
                budget.RejectionReason = null;
                break;

            case WorkflowOutcome.Rejected:
                budget.Status = ManpowerBudgetStatus.Rejected;
                budget.RejectionReason = string.IsNullOrWhiteSpace(reason)
                    ? "Rejected without a stated reason."
                    : reason.Trim();
                break;

            case WorkflowOutcome.Recalled:
                budget.Status = ManpowerBudgetStatus.Draft;
                budget.RejectionReason = null;
                break;

            case WorkflowOutcome.Pending:
            default:
                budget.Status = ManpowerBudgetStatus.Submitted;
                break;
        }
    }

    private static ManpowerBudget Require(object entity)
        => entity as ManpowerBudget
           ?? throw new InvalidOperationException(
               $"{nameof(ManpowerBudgetWorkflowStatusAdapter)} received {entity?.GetType().Name ?? "null"}.");
}
