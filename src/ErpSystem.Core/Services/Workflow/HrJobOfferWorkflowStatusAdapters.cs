using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Workflow status adapter for <see cref="JobOffer"/>.
///
/// <para>An offer belongs on the generic engine for the same reason <c>SalaryReviewProposal</c> and
/// <c>EmploymentActionProposal</c> do: <c>Draft → PendingApproval → Approved/Rejected</c> is a pure
/// approval lifecycle with a single writer, and it commits money. Everything from <c>Sent</c>
/// onwards — issuing the letter, the candidate accepting, negotiating, revising, revoking — is the
/// offer being <i>worked</i>, has several writers including the candidate themselves through the
/// anonymous token flow, and stays a direct action on the record. That is the same split as
/// <c>PipStatus</c>: the engine owns exactly the states before the work begins and nothing after.</para>
///
/// <para><c>PendingApproval</c> already existed on <c>JobOfferStatus</c>, so — as with
/// <c>StaffRequisitionStatus.Submitted</c> — no new enum member was needed. Look before you add.</para>
///
/// <para>Rejection lands on <c>Rejected</c> rather than back on <c>Draft</c>, matching the
/// requisition: <c>SubmitForApprovalAsync</c> already accepts <c>Rejected</c> as a resubmittable
/// state, and keeping it distinct tells the preparer that someone ruled against these terms rather
/// than that they never sent them. Recall returns to <c>Draft</c> — that is the preparer withdrawing
/// before anyone ruled.</para>
///
/// <para>Nothing here writes <c>Withdrawn</c>, <c>Expired</c> or any of the candidate-response
/// states. Revoking an offer is a decision someone makes about a live offer, and expiry is the
/// clock; neither is a workflow outcome.</para>
///
/// <para>Routing is a genuine policy decision, which is the other half of the test for engine
/// membership: an offer at the bottom of the band for a junior role and one above the midpoint for
/// a senior hire should not need the same approver. <c>SimpleWorkflowService</c> therefore puts the
/// salary, the band, the employment type and whether the offer sits above the band midpoint into
/// the entity context for a definition to branch on.</para>
///
/// <para>Adapters are auto-discovered by the assembly scan in
/// <c>WorkflowServiceCollectionExtensions</c>; there is no DI registration to add.</para>
/// </summary>
public sealed class JobOfferWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "JobOffer",
        "Job Offer",
        "JOB_OFFER"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, null);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
        => Apply(Require(entity), outcome, rejectionReason);

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
        => Apply(Require(entity), WorkflowOutcome.Recalled, reason);

    private static void Apply(JobOffer offer, WorkflowOutcome outcome, string? reason)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                offer.OfferStatus = JobOfferStatus.Approved;
                offer.ApprovedDate = DateTime.UtcNow;
                break;

            case WorkflowOutcome.Rejected:
                offer.OfferStatus = JobOfferStatus.Rejected;
                if (!string.IsNullOrWhiteSpace(reason))
                    offer.ApprovalRejectionReason = Trim(reason);
                break;

            case WorkflowOutcome.Recalled:
                offer.OfferStatus = JobOfferStatus.Draft;
                break;

            default:
                offer.OfferStatus = JobOfferStatus.PendingApproval;
                break;
        }
    }

    /// <summary>Trimmed to the 1000-char column the rejection reason is stored in.</summary>
    private static string Trim(string reason)
    {
        var trimmed = reason.Trim();
        return trimmed.Length > 1000 ? trimmed[..1000] : trimmed;
    }

    private static JobOffer Require(object entity)
        => entity as JobOffer ?? throw new InvalidOperationException("Expected JobOffer entity.");
}
