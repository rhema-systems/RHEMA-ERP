using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Who may rule on an HR record whose entity type has <b>no published workflow definition</b>.
/// </summary>
/// <remarks>
/// <para><b>The defect this closes, and why it is one mechanism rather than several.</b>
/// <c>WorkflowIntegrationService.SubmitAsync</c> asks <c>HasActiveApprovalWorkflowAsync</c> and,
/// when the answer is no, returns early with <c>Success = true</c> and
/// <c>WorkflowOutcome.Approved</c>. That is a deliberate "approval is not configured for this
/// entity type, use the direct lifecycle" signal, and for some callers it is the right one. Every
/// HR status adapter, however, maps <c>Approved</c> to its own <i>approved</i> status — so for any
/// HR record wired to the engine, pressing <b>Submit</b> took it from Draft to Approved in one
/// step, with no approver, no <c>CanUserApproveAsync</c> check and no segregation of duties,
/// because those rules live in <c>ApproveAsync</c>, which was never called. Each service's history
/// row recorded the transition, so afterwards it was indistinguishable from a reviewed approval.
/// No HR workflow definition is seeded anywhere in the solution, so this was the out-of-the-box
/// behaviour on every tenant.</para>
///
/// <para>It was found through recruitment (G-4.1 and G-10.1 of
/// <c>docs/HR/HR-RECRUITMENT-SYSTEM-GUIDE.md</c>) and is not a recruitment defect: several HR
/// services carried a comment asserting the <i>opposite</i> — that submit and approve are
/// "inoperable by design until a definition is published". They were not inoperable. They
/// auto-approved, which is the dangerous direction to be wrong in, and the prose was the confident
/// half.</para>
///
/// <para><b>The shape of the fix, applied per service.</b> Four changes, and all four are needed
/// together — fixing only the first makes matters worse:</para>
/// <list type="number">
///   <item><description><b>Submit</b> asks <c>HasActiveApprovalWorkflowAsync</c> and, with no
///   definition, hands the adapter <c>WorkflowOutcome.Pending</c> instead of the engine's
///   <c>Approved</c>. The record lands at the module's own submitted/pending status.</description></item>
///   <item><description><b>Approve</b> takes a no-workflow branch, because
///   <c>CanUserApproveAsync</c> returns <c>false</c> when there is no active instance — so the
///   record would be unapprovable.</description></item>
///   <item><description><b>Reject</b> likewise, for the same reason.</description></item>
///   <item><description><b>Recall</b> likewise: <c>RecallWorkflowAsync</c> answers
///   <c>"No active workflow found"</c> without an instance.</description></item>
/// </list>
/// <para>Without 2–4, a submitted record would be stuck for ever with cancellation as its only
/// exit — a worse defect than the auto-approve it replaced.</para>
///
/// <para><b>Who decides, with no definition to name an approver.</b> The module's <i>administer</i>
/// tier. This is the same "interim home" reasoning <see cref="HrPermissions"/> already records for
/// probation's three outcomes and job architecture's two approvals: until the instance-level check
/// does the real work, a management act sits on Admin. Pass the permission the module considers
/// its approval tier.</para>
///
/// <para>⚠ <b>The calling service's own segregation-of-duties check is the stronger half of the
/// gate and still runs.</b> Holding the permission must never let you approve your own request.
/// This helper answers "may this person approve <i>anything</i> in this module", never "may this
/// person approve <i>this</i>".</para>
///
/// <para>⚠ Authority resolves from <see cref="HrPermissions.RoleGrants"/>, not from the database,
/// so a permission granted directly to a custom role is not seen — the limitation
/// <see cref="HrPermissions.RolesGrantAny"/> documents. That is acceptable because this is the
/// <i>unconfigured</i> path: a tenant wanting a named approver chain publishes a definition, and
/// then none of this code runs. If a tenant needs custom-role approvers, publish the definition
/// rather than widening this.</para>
///
/// <para><b>The shared fallback in <c>WorkflowIntegrationService</c> is deliberately left
/// alone.</b> It is used by procurement and quantity survey as well, whose callers were not
/// audited here. Changing it centrally would alter nine applications at once on the strength of an
/// HR finding. Each HR service opts out for itself, which is greppable: search for
/// <c>HasActiveApprovalWorkflowAsync</c> to see which services have been through this.</para>
/// </remarks>
public static class HrWorkflowFallbackAuthority
{
    /// <summary>
    /// True when the caller may rule on an unconfigured record in the module owning
    /// <paramref name="approvalPermissions"/>.
    /// </summary>
    public static bool CanRuleWithoutWorkflow(
        ICurrentUserProvider currentUser,
        params string[] approvalPermissions)
        => currentUser.HasRole(Constants.Roles.SuperAdmin) ||
           HrPermissions.RolesGrantAny(currentUser.Roles, approvalPermissions);

    /// <summary>
    /// Refuses unless the caller may rule on an unconfigured record.
    /// </summary>
    /// <param name="currentUser">The caller.</param>
    /// <param name="action">Filled into the message, e.g. <c>"approve a staff movement"</c>.</param>
    /// <param name="approvalPermissions">The module's approval tier, e.g.
    /// <c>HrPermissions.AdministerMovements</c>.</param>
    public static void EnsureCanRuleWithoutWorkflow(
        ICurrentUserProvider currentUser,
        string action,
        params string[] approvalPermissions)
    {
        if (CanRuleWithoutWorkflow(currentUser, approvalPermissions)) return;

        throw new UnauthorizedAccessException(
            $"No approval workflow is published for this record, so authority to {action} falls to " +
            "the administrators of this area. Ask them to decide, or publish an approval workflow " +
            "definition so the approver is named on the record.");
    }
}
