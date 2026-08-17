using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Workflow status adapter for <see cref="StaffTravelRequest"/>.
///
/// <para>Travel belongs on the generic engine for the same reason a movement does: Draft →
/// Submitted → Approved is a pure approval lifecycle with a single writer, and everything after
/// approval — booking, travelling, claiming, settling the advance — is the trip being carried out,
/// has several writers, and stays a direct action on the record. The adapter owns exactly the three
/// states before that and nothing after.</para>
///
/// <para><b>What this replaces.</b> The area shipped its own four-table approval chain —
/// <c>StaffTravelApprovalWorkflowTemplate</c>, <c>...Step</c>, <c>...Instance</c>,
/// <c>...Decision</c> — which had never executed: zero steps, zero instances, zero decisions on the
/// reference database. It was also an actor hole, because <c>RecordDecisionAsync</c> took the
/// approver from the request body, so a caller could record a decision in someone else's name. The
/// engine resolves the approver from the authenticated user against the published definition, which
/// is the whole point of moving.</para>
///
/// <para><b>Routing is a real policy decision here</b>, which is the other half of the test for
/// putting something on the engine. A GHS 400 taxi to Kumasi and a two-week trip to Lagos costing
/// GHS 40,000 are not the same decision, so <c>SimpleWorkflowService</c> puts the estimated cost,
/// the currency, whether the trip is international, the risk level and the priority into the entity
/// context. A role-based chain could not express any of that.</para>
///
/// <para><b>No new enum member.</b> <see cref="StaffTravelRequestStatus"/> already has
/// <c>Submitted</c>, which means exactly "out for approval" — the same call the requisition wiring
/// made. Look before you add.</para>
///
/// <para><b>Rejection is terminal here.</b> It lands on <c>Rejected</c> rather than returning to
/// Draft, and unlike a requisition the service does not accept a rejected request back:
/// <c>SubmitAsync</c> allows only <c>Draft</c> or <c>ReturnedForRevision</c>. Travel dates move, so
/// reviving a refused request is usually wrong — raise a fresh one. <c>ReturnedForRevision</c> is
/// the route for "fix this and resend", and recall is the requester's own way back to Draft.</para>
///
/// <para>⚠ The rejection reason is written to <c>CancellationReason</c>, which the entity also uses
/// for cancellations — there is no separate rejection column. Status disambiguates the two, and
/// adding a column was not worth a migration in this slice, but a later reader should not mistake
/// the field name for the meaning.</para>
///
/// <para>Adapters are auto-discovered by the assembly scan in
/// <c>WorkflowServiceCollectionExtensions</c>; there is no DI registration to add.</para>
/// </summary>
public sealed class StaffTravelRequestWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "StaffTravelRequest",
        "Staff Travel Request",
        "STAFF_TRAVEL_REQUEST"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome, null);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
        => Apply(Require(entity), outcome, rejectionReason);

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
        => Apply(Require(entity), WorkflowOutcome.Recalled, reason);

    private static void Apply(StaffTravelRequest request, WorkflowOutcome outcome, string? reason)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                request.Status = StaffTravelRequestStatus.Approved;
                request.ApprovedAt = DateTime.UtcNow;
                break;

            case WorkflowOutcome.Rejected:
                request.Status = StaffTravelRequestStatus.Rejected;
                if (!string.IsNullOrWhiteSpace(reason))
                    request.CancellationReason = reason.Trim();
                break;

            case WorkflowOutcome.Recalled:
                // Back to the requester, and the reason is cleared: a recalled request was never
                // refused by anybody, and leaving a stale reason on it would say it was.
                request.Status = StaffTravelRequestStatus.Draft;
                request.CancellationReason = null;
                request.SubmittedAt = null;
                break;

            default:
                request.Status = StaffTravelRequestStatus.Submitted;
                break;
        }
    }

    private static StaffTravelRequest Require(object entity)
        => entity as StaffTravelRequest ?? throw new InvalidOperationException("Expected StaffTravelRequest entity.");
}
