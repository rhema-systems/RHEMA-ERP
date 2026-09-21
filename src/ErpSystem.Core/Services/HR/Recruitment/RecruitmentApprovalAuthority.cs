using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;

namespace ErpSystem.Core.Services.HR.Recruitment;

/// <summary>
/// Who may approve a requisition or an offer when <b>no workflow definition is published</b> for
/// its entity type.
/// </summary>
/// <remarks>
/// <para><b>Why this exists (G-4.1 / G-10.1, 2026-09-15).</b>
/// <c>WorkflowIntegrationService.SubmitAsync</c> returns <c>WorkflowOutcome.Approved</c> whenever
/// <c>HasActiveApprovalWorkflowAsync</c> says no active definition exists — a deliberate
/// "approval is not configured for this entity type, use the direct lifecycle" signal that nine
/// modules share and that is not being changed here. The two recruitment adapters map that outcome
/// to their own <i>approved</i> status, so pressing <b>Submit</b> took a requisition Draft →
/// Approved and an offer Draft → Approved-with-an-ApprovedDate, in one step, with no approver and
/// no segregation-of-duties check — because that check lives in <c>ApproveAsync</c>, which was
/// never reached. For the offer it is the step that authorises sending legally-meaningful terms —
/// salary, start date, notice, probation — to a person outside the organisation.
///
/// <para>⚠ <b>This said "no <c>StaffRequisition</c> or <c>JobOffer</c> definition is seeded
/// anywhere in the solution, so that was the out-of-the-box behaviour on every tenant". False</b>
/// (corrected 2026-09-16): both are seeded, published and active by
/// <c>EnsureHrWorkflowsSeededAsync</c>. On a seeded tenant this path never ran. It is kept as
/// defence in depth — see <see cref="HrWorkflowFallbackAuthority"/> for why that is worth having.
/// Note also that the offer's <b>segregation-of-duties check was genuinely missing</b> regardless
/// of any definition, and was added in the same change.</para></para>
///
/// <para><b>What changed.</b> Both services now ask <c>HasActiveApprovalWorkflowAsync</c> before
/// applying a submit outcome, and hand the adapter <c>Pending</c> rather than the engine's
/// <c>Approved</c> when the answer is no. The record lands at <c>Submitted</c> /
/// <c>PendingApproval</c> and waits for a person.</para>
///
/// <para><b>Why the rest of the flow needed changing too.</b> Making Submit stop at a pending
/// state would have created a worse defect than the one it fixed, because every other step in the
/// chain asks the engine a question the engine cannot answer without an instance:
/// <c>CanUserApproveAsync</c> returns <c>false</c> when there is no active instance, and
/// <c>RecallWorkflowAsync</c> returns <c>"No active workflow found"</c>. Approve, reject and
/// recall would all have failed, and the record would have been stuck at Submitted for ever — the
/// shape of G-4.2, which this programme is also closing. So each of those steps now takes a
/// no-workflow branch: skip the engine, check authority here, and apply the outcome directly.</para>
///
/// <para><b>Who decides, with no definition to name an approver.</b>
/// <see cref="HrPermissions.ApproveRecruitment"/>. This started on the recruitment <i>administer</i>
/// tier and was moved once the mechanism went HR-wide: the administer tiers are
/// destructive-operations permissions, so approving on them would have widened HR's destructive
/// reach as a side effect. The <c>.Approve</c> block exists for this and nothing else, and is
/// granted to the HR desk (<see cref="HrPermissions.HrStaffGrants"/>) — so the approval is
/// reachable by exactly the people who run the function, and not by the manager who raised the
/// request.</para>
///
/// <para>⚠ <b>The segregation-of-duties checks in the calling services still run and are the
/// stronger half of this gate.</b> Holding the permission does not let you approve your own
/// requisition. Keep it that way: this helper answers "may this person approve <i>anything</i>",
/// never "may this person approve <i>this</i>".</para>
///
/// <para>⚠ Authority resolves from <see cref="HrPermissions.RoleGrants"/>, not from the database,
/// so a permission granted directly to a custom role is not seen here — the limitation
/// <see cref="HrPermissions.RolesGrantAny"/> documents. That is acceptable because this is the
/// <i>unconfigured</i> path: a tenant that wants a named approver chain publishes a definition,
/// and then none of this code runs. If a tenant needs custom-role approvers without a definition,
/// publish the definition instead of widening this.</para>
/// </remarks>
/// <para>⚠ <b>This is recruitment's binding of a mechanism that turned out to be HR-wide.</b> The
/// finding was traced through recruitment first, then found in roughly twenty other HR services
/// that call the same engine. The implementation therefore lives in
/// <see cref="HrWorkflowFallbackAuthority"/>, which documents the full defect and the four-part
/// shape of the fix; this type stays as the recruitment-shaped name the recruitment services call,
/// so the permission it pins is stated once rather than repeated at every call site.</para>
public static class RecruitmentApprovalAuthority
{
    /// <summary>
    /// True when the caller may rule on a recruitment record that has no published workflow
    /// definition behind it.
    /// </summary>
    public static bool CanRuleWithoutWorkflow(ICurrentUserProvider currentUser)
        => HrWorkflowFallbackAuthority.CanRuleWithoutWorkflow(
            currentUser, HrPermissions.ApproveRecruitment);

    /// <summary>
    /// Refuses unless the caller may rule on an unconfigured recruitment record.
    /// </summary>
    /// <param name="currentUser">The caller.</param>
    /// <param name="action">Filled into the message, e.g. <c>"approve a requisition"</c>.</param>
    public static void EnsureCanRuleWithoutWorkflow(ICurrentUserProvider currentUser, string action)
        => HrWorkflowFallbackAuthority.EnsureCanRuleWithoutWorkflow(
            currentUser, action, HrPermissions.ApproveRecruitment);
}
