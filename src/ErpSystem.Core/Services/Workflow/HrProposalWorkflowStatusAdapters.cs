using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Workflow status adapters for the two intake records an approved appraisal outcome raises:
/// <see cref="SalaryReviewProposal"/> and <see cref="EmploymentActionProposal"/>.
///
/// <para>Both belong on the generic engine for the same reason <c>AppraisalTemplate</c> does and
/// <c>GoalStatus</c> and <c>AppraisalStatus</c> do not: each is a pure approval lifecycle with a
/// single writer — Proposed → Approved/Rejected → Applied/Actioned — and the routing is genuinely
/// a policy decision. Who signs off a 3% merit increase is not who signs off a termination, and
/// that is exactly what a conditional workflow definition is for: <c>proposedPercent</c>,
/// <c>proposedAmount</c> and <c>actionType</c> are put into the entity context so a definition
/// can branch on them.</para>
///
/// <para>The terminal step — Applied for a pay change, Actioned for an employment action — stays
/// off the engine. It records that another module has done the work, not that anyone approved
/// it, so it remains an explicit HR action on the proposal.</para>
///
/// <para>Adapters are auto-discovered by the assembly scan in
/// <c>WorkflowServiceCollectionExtensions</c>; there is no DI registration to add.</para>
/// </summary>
public sealed class SalaryReviewProposalWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "SalaryReviewProposal",
        "Salary Review Proposal",
        "SALARY_REVIEW_PROPOSAL"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, null);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
        => Apply(Require(entity), outcome, rejectionReason);

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
        => Apply(Require(entity), WorkflowOutcome.Recalled, reason);

    private static void Apply(SalaryReviewProposal proposal, WorkflowOutcome outcome, string? reason)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                proposal.Status = SalaryReviewProposalStatus.Approved;
                break;
            case WorkflowOutcome.Rejected:
                proposal.Status = SalaryReviewProposalStatus.Rejected;
                if (!string.IsNullOrWhiteSpace(reason))
                    proposal.Notes = Append(proposal.Notes, $"Rejected: {reason.Trim()}");
                break;
            case WorkflowOutcome.Recalled:
                // Back to the proposer's hands so the figure can be reworked and re-submitted.
                proposal.Status = SalaryReviewProposalStatus.Proposed;
                break;
            default:
                proposal.Status = SalaryReviewProposalStatus.PendingApproval;
                break;
        }
    }

    /// <summary>Notes are a 1000-char column, so an appended reason is trimmed to fit.</summary>
    internal static string Append(string? existing, string addition)
    {
        var combined = string.IsNullOrWhiteSpace(existing) ? addition : $"{existing}\n{addition}";
        return combined.Length > 1000 ? combined[..1000] : combined;
    }

    private static SalaryReviewProposal Require(object entity)
        => entity as SalaryReviewProposal ?? throw new InvalidOperationException("Expected SalaryReviewProposal entity.");
}

/// <inheritdoc cref="SalaryReviewProposalWorkflowStatusAdapter"/>
public sealed class EmploymentActionProposalWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "EmploymentActionProposal",
        "Employment Action Proposal",
        "EMPLOYMENT_ACTION_PROPOSAL"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, null);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
        => Apply(Require(entity), outcome, rejectionReason);

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
        => Apply(Require(entity), WorkflowOutcome.Recalled, reason);

    private static void Apply(EmploymentActionProposal proposal, WorkflowOutcome outcome, string? reason)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                proposal.Status = EmploymentActionProposalStatus.Approved;
                break;
            case WorkflowOutcome.Rejected:
                proposal.Status = EmploymentActionProposalStatus.Rejected;
                if (!string.IsNullOrWhiteSpace(reason))
                    proposal.Notes = SalaryReviewProposalWorkflowStatusAdapter.Append(proposal.Notes, $"Rejected: {reason.Trim()}");
                break;
            case WorkflowOutcome.Recalled:
                proposal.Status = EmploymentActionProposalStatus.Proposed;
                break;
            default:
                proposal.Status = EmploymentActionProposalStatus.PendingApproval;
                break;
        }
    }

    private static EmploymentActionProposal Require(object entity)
        => entity as EmploymentActionProposal ?? throw new InvalidOperationException("Expected EmploymentActionProposal entity.");
}
